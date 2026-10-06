#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;

namespace Release;

/// <summary>
/// Packs the global tool (forge/Tool) with the release version stamped in (I1), for the
/// <see cref="ReleaseSink.GlobalTool"/> sink.
/// </summary>
public static class GlobalToolPackager
{
    /// <summary>The tool's package id (design/20-contract.md, Global tool).</summary>
    public const string PackageId = "BuildAgent.Tool";

    /// <summary>Packs <paramref name="projectPath"/> and returns the path of the package it wrote.</summary>
    public static async Task<string> PackAsync(string projectPath, ReleaseVersion version, string outputDirectory, ICommandRunner runner)
    {
        Directory.CreateDirectory(outputDirectory);
        var packageVersion = version.ToPackageString();

        await runner.RunCheckedAsync(
            "dotnet",
            new[] { "pack", projectPath, "--configuration", "Release", $"-p:ReleaseVersion={packageVersion}", "--output", outputDirectory })
            .ConfigureAwait(false);

        var package = Path.Combine(outputDirectory, $"{PackageId}.{packageVersion}.nupkg");
        if (!File.Exists(package))
        {
            throw new InvalidOperationException($"Packing the global tool wrote no {Path.GetFileName(package)} to {outputDirectory}.");
        }

        return package;
    }
}
