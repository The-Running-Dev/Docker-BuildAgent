#nullable enable

using System.Threading.Tasks;

using Surface;

namespace Release;

/// <summary>
/// The claim collaborator: a GitHub draft release bound to a commit SHA. This is deliberately
/// narrower than <c>Services.IGitHubService</c> (which does silent create-or-update by tag name)
/// because claim-first semantics (I3, I5) require telling "no claim yet" apart from "a claim
/// already exists" before any write happens.
/// </summary>
public interface IReleaseClaimStore
{
    /// <summary>The existing claim for a version, draft or published, or null if none exists.</summary>
    Task<ReleaseClaim?> FindClaimAsync(ReleaseVersion version);

    /// <summary>
    /// Creates a new draft release bound to <paramref name="commitSha"/>. This is the first write
    /// of a release (I3). Throws <see cref="ReleaseException"/> with
    /// <see cref="ReleaseErrorCode.ClaimCreationFailed"/> if the draft cannot be created.
    /// </summary>
    Task<ReleaseClaim> CreateDraftAsync(ReleaseVersion version, string commitSha, string notes, SurfaceManifest manifest);

    /// <summary>Transitions a claim from Draft to Published. The last write of a successful release.</summary>
    Task PublishAsync(ReleaseClaim claim);
}
