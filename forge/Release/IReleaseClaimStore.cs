#nullable enable

using System.Threading.Tasks;

using Surface;

namespace Release;

/// <summary>
/// The claim ref <c>refs/release-claims/v&lt;version&gt;</c> after
/// <see cref="IReleaseClaimStore.CreateClaimRefAsync"/>: the commit it points at, and whether that
/// call created it.
/// </summary>
public sealed record ClaimRef(string CommitSha, bool Created);

/// <summary>
/// The claim collaborator: the claim ref <c>refs/release-claims/v&lt;version&gt;</c> and a GitHub
/// draft release, both bound to a commit SHA. This is deliberately narrower than
/// <c>Services.IGitHubService</c> (which does silent create-or-update by tag name) because
/// claim-first semantics (I3, I5) require telling "no claim yet" apart from "a claim already
/// exists" before any write happens.
/// </summary>
public interface IReleaseClaimStore
{
    /// <summary>The commit the version's claim ref points at, or null if the ref does not exist.</summary>
    Task<string?> FindClaimRefAsync(ReleaseVersion version);

    /// <summary>
    /// Creates the version's claim ref at <paramref name="commitSha"/> unless it already exists,
    /// and returns the commit the ref points at afterwards. The refs API refuses a ref that exists,
    /// so of two runs claiming one version only one creates it (I13). Throws
    /// <see cref="ReleaseException"/> with <see cref="ReleaseErrorCode.ClaimCreationFailed"/> if the
    /// ref can be neither created nor read.
    /// </summary>
    Task<ClaimRef> CreateClaimRefAsync(ReleaseVersion version, string commitSha);

    /// <summary>The existing release for a version, draft or published, or null if none exists.</summary>
    Task<ReleaseClaim?> FindClaimAsync(ReleaseVersion version);

    /// <summary>
    /// Creates a new draft release bound to <paramref name="commitSha"/>. It follows the claim ref,
    /// which is the first write of a release (I3). Throws <see cref="ReleaseException"/> with
    /// <see cref="ReleaseErrorCode.ClaimCreationFailed"/> if the draft cannot be created.
    /// </summary>
    Task<ReleaseClaim> CreateDraftAsync(ReleaseVersion version, string commitSha, string notes, SurfaceManifest manifest);

    /// <summary>
    /// Records <paramref name="identity"/> for <paramref name="sink"/> in the draft claim and returns
    /// the updated claim. Called just before that sink is written, and never for a sink that already
    /// holds the version, so a recorded identity is replaced only while its sink is unwritten.
    /// </summary>
    Task<ReleaseClaim> RecordIdentityAsync(ReleaseClaim claim, ReleaseSink sink, string identity);

    /// <summary>Transitions a claim from Draft to Published. The last write of a successful release.</summary>
    Task PublishAsync(ReleaseClaim claim);
}
