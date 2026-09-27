using System;
using System.Collections.Generic;

using Parameters;

namespace Config;

/// <summary>
/// The closed five-value `buildType` set (I29, declared at scripts/nuke/build.ps1's `$type`
/// ValidateSet) and each type's backing `*Params` class for key/value validation. `node-template`
/// is an accepted build type with no backing class (design/30-slices.md § S6, Withdrawn — the
/// PowerShell-only node-template flow has no `*Params` class today, and giving it one is outside
/// this slice's Touches list); a project file for it has an empty known-parameter set, so any
/// `parameters:` key it declares is <see cref="ConfigErrorCode.UnknownKey"/>.
/// </summary>
internal static class BuildTypeCatalog
{
    public static readonly IReadOnlyList<string> AcceptedBuildTypes = new[]
    {
        "docker", "node", "node-in-docker", "node-template", "forge",
    };

    public static bool TryGetParamsType(string buildType, out Type? paramsType)
    {
        switch (buildType)
        {
            case "docker":
                paramsType = typeof(DockerParams);
                return true;
            case "node":
                paramsType = typeof(NodeParams);
                return true;
            case "node-in-docker":
                paramsType = typeof(NodeInDockerParams);
                return true;
            case "node-template":
                paramsType = null;
                return true;
            case "forge":
                paramsType = typeof(ForgeParams);
                return true;
            default:
                paramsType = null;
                return false;
        }
    }
}
