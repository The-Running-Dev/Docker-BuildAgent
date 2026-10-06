#nullable enable

using Surface;

namespace Release;

/// <summary>
/// Turns the surface gate's result into the release's decision (design/10-design.md, control
/// flow 2, step 3). A missing baseline fails every release except 2.0.0, the first gated release,
/// which has no earlier release carrying a surface manifest and is judged by its migration guide.
/// </summary>
public static class ReleaseSurfaceCheck
{
    /// <summary>
    /// Returns the candidate manifest the release publishes, or throws a
    /// <see cref="ReleaseException"/> with <see cref="ReleaseErrorCode.SurfaceGateFailed"/> naming
    /// the gate's code.
    /// </summary>
    public static SurfaceManifest Accept(SurfaceGateResult result, ReleaseVersion version)
    {
        if (result.Success)
        {
            return result.Candidate;
        }

        if (result.ErrorCode == SurfaceErrorCode.BaselineMissing && result.Candidate != null && IsFirstGatedRelease(version))
        {
            return result.Candidate;
        }

        throw new ReleaseException(ReleaseErrorCode.SurfaceGateFailed, null, $"Surface gate failed ({result.ErrorCode}): {result.Message}");
    }

    /// <summary>True for 2.0.0 and its pre-releases: the releases that may have no baseline.</summary>
    public static bool IsFirstGatedRelease(ReleaseVersion version) =>
        version is { Major: 2, Minor: 0, Patch: 0 };
}
