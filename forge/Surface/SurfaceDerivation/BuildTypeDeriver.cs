#nullable enable

using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Surface.SurfaceDerivation;

/// <summary>
/// Derives one BuildType item per accepted value of the `build &lt;type&gt;` CLI, read from the
/// `$type` parameter's `[ValidateSet(...)]` at scripts/nuke/build.ps1 (I29: the accepted
/// build-type set is closed and declared there).
/// </summary>
internal static class BuildTypeDeriver
{
    public static List<SurfaceItem> Derive(string rootDirectory)
    {
        var path = Path.Combine(rootDirectory, "scripts", "nuke", "build.ps1");
        if (!File.Exists(path))
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"Build script not found: '{path}'.");
        }

        var source = File.ReadAllText(path);
        var body = PowerShellParamBlockParser.ExtractParamBlock(source);
        var chunks = PowerShellParamBlockParser.SplitTopLevelParameters(body);

        foreach (var chunk in chunks)
        {
            var parsed = PowerShellParamBlockParser.ParseParameter(chunk);
            if (parsed.Name != "type") continue;

            if (parsed.ValidateSet == null || parsed.ValidateSet.Count == 0)
            {
                throw new SurfaceException(SurfaceErrorCode.DerivationFailed, "The '$type' parameter has no ValidateSet; the accepted build-type set could not be derived.");
            }

            return parsed.ValidateSet
                .Select(buildType => new SurfaceItem(SurfaceItemKind.BuildType, buildType, "accepted", null, null))
                .ToList();
        }

        throw new SurfaceException(SurfaceErrorCode.DerivationFailed, "No '$type' parameter found in the build script's param block.");
    }
}
