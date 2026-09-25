#nullable enable

namespace Release;

/// <summary>The record that settles whether a version exists (I5). A GitHub draft release bound to a commit SHA.</summary>
public enum ClaimState { Draft, Published }

/// <summary>
/// The enum's numeric values are the publication order and are load-bearing — <see cref="ImageLatestTag"/>
/// is last (I4).
/// </summary>
public enum ReleaseSink
{
    ImageVersionedTag = 1,
    GlobalTool = 2,
    PowerShellModule = 3,
    ImageLatestTag = 4,
}

/// <summary>
/// The claim carries no per-sink completion state; what that costs, and what a resume may
/// therefore assume, is design/20-contract.md "## Unresolved" U-6.
/// </summary>
public sealed record ReleaseClaim(
    ReleaseVersion Version,
    string CommitSha,
    ClaimState State,
    string Notes,
    Surface.SurfaceManifest CandidateManifest);
