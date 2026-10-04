#nullable enable

using System.Collections.Generic;

namespace Release;

/// <summary>The record that settles whether a version exists (I5). A claim ref and a GitHub draft release bound to a commit SHA.</summary>
public enum ClaimState { Draft, Published }

/// <summary>
/// The enum's numeric values are the publication order and are load-bearing. The versioned sinks
/// are written in this order before the release is published; <see cref="ImageLatestTag"/> is
/// last and moves only after the release is published (I4).
/// </summary>
public enum ReleaseSink
{
    ImageVersionedTag = 1,
    GlobalTool = 2,
    PowerShellModule = 3,
    ImageLatestTag = 4,
}

/// <summary>
/// A claim for one version. <see cref="ArtifactIdentities"/> holds each versioned sink's artifact
/// identity, recorded just before that sink is written and never replaced once the sink holds the
/// version: the image digest, and each package's <see cref="PackageContentHash"/>. A resume skips
/// a sink only when the identity it holds equals the recorded one (I5).
/// </summary>
public sealed record ReleaseClaim(
    ReleaseVersion Version,
    string CommitSha,
    ClaimState State,
    string Notes,
    Surface.SurfaceManifest CandidateManifest)
{
    private static readonly IReadOnlyDictionary<ReleaseSink, string> NoIdentities = new Dictionary<ReleaseSink, string>();

    public IReadOnlyDictionary<ReleaseSink, string> ArtifactIdentities { get; init; } = NoIdentities;

    /// <summary>The identity recorded for <paramref name="sink"/>, or null if none is recorded.</summary>
    public string? IdentityOf(ReleaseSink sink) => ArtifactIdentities.TryGetValue(sink, out var identity) ? identity : null;

    /// <summary>This claim with <paramref name="identity"/> recorded for <paramref name="sink"/>.</summary>
    public ReleaseClaim WithIdentity(ReleaseSink sink, string identity) =>
        this with { ArtifactIdentities = new Dictionary<ReleaseSink, string>(ArtifactIdentities) { [sink] = identity } };
}
