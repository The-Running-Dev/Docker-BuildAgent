#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Release;

/// <summary>
/// Packs the Docker-BuildAgent module as the package the PowerShell Gallery takes: the module
/// manifest stamped with the release version, its root module, and a nuspec carrying the tags the
/// Gallery indexes. The manifest's <c>ModuleVersion</c> is the version's core, and a pre-release
/// label goes in <c>PSData.Prerelease</c>, which is how PowerShellGet names a pre-release module.
/// The tests and the parameter-table generator stay out: the module ships its manifest and root
/// module only.
/// </summary>
public static class PowerShellModulePackager
{
    public const string ModuleName = "Docker-BuildAgent";

    private static readonly DateTimeOffset EntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly Regex ModuleVersionLine = new(@"(?m)^(\s*ModuleVersion\s*=\s*)'[^']*'", RegexOptions.CultureInvariant);
    private static readonly Regex PrereleaseLine = new(@"(?m)^\s*Prerelease\s*=.*\r?\n", RegexOptions.CultureInvariant);
    private static readonly Regex PsDataOpening = new(@"(?m)^(\s*)PSData\s*=\s*@\{[ \t]*(\r?\n)", RegexOptions.CultureInvariant);

    /// <summary>Writes <c>Docker-BuildAgent.&lt;version&gt;.nupkg</c> into <paramref name="outputDirectory"/> and returns its path.</summary>
    public static string Pack(string moduleDirectory, ReleaseVersion version, string outputDirectory)
    {
        var manifestPath = Path.Combine(moduleDirectory, $"{ModuleName}.psd1");
        var manifest = Stamp(File.ReadAllText(manifestPath), version);
        var rootModule = ReadSetting(manifest, "RootModule");

        Directory.CreateDirectory(outputDirectory);
        var packagePath = Path.Combine(outputDirectory, $"{ModuleName}.{version.ToPackageString()}.nupkg");
        using var file = File.Create(packagePath);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);

        AddText(archive, "_rels/.rels", Relationships());
        AddText(archive, "[Content_Types].xml", ContentTypes());
        AddText(archive, $"{ModuleName}.nuspec", Nuspec(manifest, version));
        AddText(archive, $"{ModuleName}.psd1", manifest);
        AddBytes(archive, rootModule, File.ReadAllBytes(Path.Combine(moduleDirectory, rootModule)));

        return packagePath;
    }

    /// <summary>The manifest text with <paramref name="version"/> set as its module version and pre-release label.</summary>
    public static string Stamp(string manifest, ReleaseVersion version)
    {
        if (!ModuleVersionLine.IsMatch(manifest))
        {
            throw new InvalidOperationException("The module manifest has no ModuleVersion line to stamp.");
        }

        var stamped = ModuleVersionLine.Replace(manifest, $"${{1}}'{version.Major}.{version.Minor}.{version.Patch}'", 1);
        stamped = PrereleaseLine.Replace(stamped, string.Empty);
        if (version.PreRelease == null)
        {
            return stamped;
        }

        if (!PsDataOpening.IsMatch(stamped))
        {
            throw new InvalidOperationException("The module manifest has no PSData block to hold the pre-release label.");
        }

        return PsDataOpening.Replace(stamped, match =>
            $"{match.Value}{match.Groups[1].Value}    Prerelease = '{version.PreRelease}'{match.Groups[2].Value}", 1);
    }

    private static string Nuspec(string manifest, ReleaseVersion version)
    {
        var functions = ReadList(manifest, "FunctionsToExport");
        var editions = ReadList(manifest, "CompatiblePSEditions");
        var tags = new[] { "PSModule", "PSIncludes_Function" }
            .Concat(functions.SelectMany(f => new[] { $"PSFunction_{f}", $"PSCommand_{f}" }))
            .Concat(editions.Select(e => $"PSEdition_{e}"));

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd">
              <metadata>
                <id>{ModuleName}</id>
                <version>{version.ToPackageString()}</version>
                <authors>{Xml(ReadSetting(manifest, "Author"))}</authors>
                <owners>{Xml(ReadSetting(manifest, "CompanyName"))}</owners>
                <requireLicenseAcceptance>false</requireLicenseAcceptance>
                <description>{Xml(ReadSetting(manifest, "Description"))}</description>
                <copyright>{Xml(ReadSetting(manifest, "Copyright"))}</copyright>
                <projectUrl>https://build-agent.subzerodev.com/docs/powershell-module</projectUrl>
                <tags>{Xml(string.Join(' ', tags))}</tags>
              </metadata>
            </package>
            """;
    }

    private static string ReadSetting(string manifest, string name)
    {
        var match = Regex.Match(manifest, $@"(?m)^\s*{name}\s*=\s*'([^']*)'", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException($"The module manifest has no {name} setting.");
    }

    private static string[] ReadList(string manifest, string name)
    {
        var match = Regex.Match(manifest, $@"(?m)^\s*{name}\s*=\s*@\(([^)]*)\)", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidOperationException($"The module manifest has no {name} list.");
        }

        return Regex.Matches(match.Groups[1].Value, "'([^']+)'").Select(m => m.Groups[1].Value).ToArray();
    }

    private static string Relationships() => """
        <?xml version="1.0" encoding="utf-8"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/Docker-BuildAgent.nuspec" Id="R0" />
        </Relationships>
        """;

    private static string ContentTypes() => """
        <?xml version="1.0" encoding="utf-8"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="nuspec" ContentType="application/octet" />
          <Default Extension="psd1" ContentType="application/octet" />
          <Default Extension="psm1" ContentType="application/octet" />
        </Types>
        """;

    private static string Xml(string value) => SecurityElement.Escape(value);

    private static void AddText(ZipArchive archive, string path, string text) => AddBytes(archive, path, Utf8NoBom.GetBytes(text));

    private static void AddBytes(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = EntryTime;
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }
}
