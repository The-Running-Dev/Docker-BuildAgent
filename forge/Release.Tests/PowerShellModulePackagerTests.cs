#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

using Xunit;

namespace Release.Tests;

public sealed class PowerShellModulePackagerTests : IDisposable
{
    private const string Manifest = """
        @{
            RootModule           = 'Docker-BuildAgent.psm1'
            ModuleVersion        = '2.0.0'
            PrivateData          = @{
                PSData = @{
                    ReleaseNotes = @('notes')
                }
            }
        }
        """;

    private readonly string _work = Path.Combine(Path.GetTempPath(), "module-packager-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    [Fact]
    public void Stamp_SetsTheVersion_AndNoLabelForAStableRelease()
    {
        var stamped = PowerShellModulePackager.Stamp(Manifest, ReleaseVersion.Parse("2.1.0"));

        Assert.Contains("ModuleVersion        = '2.1.0'", stamped);
        Assert.DoesNotContain("Prerelease", stamped);
    }

    [Fact]
    public void Stamp_SetsTheCore_AndPutsTheLabelInPSData()
    {
        var stamped = PowerShellModulePackager.Stamp(Manifest, ReleaseVersion.Parse("2.1.0-rc1"));

        Assert.Contains("ModuleVersion        = '2.1.0'", stamped);
        Assert.Matches(@"PSData = @\{\r?\n\s*Prerelease = 'rc1'\r?\n\s*ReleaseNotes", stamped);
    }

    [Fact]
    public void Stamp_ReplacesALabelTheManifestAlreadyHas()
    {
        var labelled = PowerShellModulePackager.Stamp(Manifest, ReleaseVersion.Parse("2.1.0-rc1"));

        var stable = PowerShellModulePackager.Stamp(labelled, ReleaseVersion.Parse("2.1.0"));
        var relabelled = PowerShellModulePackager.Stamp(labelled, ReleaseVersion.Parse("2.1.0-rc2"));

        Assert.DoesNotContain("Prerelease", stable);
        Assert.Single(relabelled.Split('\n'), line => line.Contains("Prerelease"));
        Assert.Contains("Prerelease = 'rc2'", relabelled);
    }

    [Fact]
    public void Stamp_Fails_WithoutAModuleVersionLine()
    {
        Assert.Throws<InvalidOperationException>(() => PowerShellModulePackager.Stamp("@{ }", ReleaseVersion.Parse("2.1.0")));
    }

    [Fact]
    public void Pack_ShipsTheStampedManifestAndRootModule_WithTheGalleryTags()
    {
        var moduleDirectory = Path.Combine(RepoRoot(), "scripts", "powershell-module");

        var package = PowerShellModulePackager.Pack(moduleDirectory, ReleaseVersion.Parse("2.1.0-rc1"), _work);

        Assert.Equal(Path.Combine(_work, "Docker-BuildAgent.2.1.0-rc1.nupkg"), package);
        using var archive = ZipFile.OpenRead(package);
        Assert.Equal(
            new[] { "Docker-BuildAgent.nuspec", "Docker-BuildAgent.psd1", "Docker-BuildAgent.psm1", "[Content_Types].xml", "_rels/.rels" },
            archive.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(moduleDirectory, "Docker-BuildAgent.psm1")),
            Read(archive, "Docker-BuildAgent.psm1"));
        var manifest = System.Text.Encoding.UTF8.GetString(Read(archive, "Docker-BuildAgent.psd1"));
        Assert.Contains("ModuleVersion        = '2.1.0'", manifest);
        Assert.Contains("Prerelease = 'rc1'", manifest);

        var nuspec = XDocument.Parse(System.Text.Encoding.UTF8.GetString(Read(archive, "Docker-BuildAgent.nuspec")));
        var ns = nuspec.Root!.Name.Namespace;
        var metadata = nuspec.Root.Element(ns + "metadata")!;
        Assert.Equal("Docker-BuildAgent", metadata.Element(ns + "id")!.Value);
        Assert.Equal("2.1.0-rc1", metadata.Element(ns + "version")!.Value);
        var tags = metadata.Element(ns + "tags")!.Value.Split(' ');
        Assert.Contains("PSModule", tags);
        Assert.Contains("PSFunction_Invoke-Build", tags);
        Assert.Contains("PSCommand_Set-BuildAgentConfig", tags);
        Assert.Contains("PSEdition_Core", tags);
        Assert.Contains("PSEdition_Desktop", tags);
    }

    private static byte[] Read(ZipArchive archive, string path)
    {
        using var stream = archive.GetEntry(path)!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
