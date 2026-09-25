#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using Octokit;

using Surface;

namespace Release;

/// <summary>
/// <see cref="IReleaseClaimStore"/> backed by GitHub draft releases, via Octokit. Deliberately
/// narrower than <c>Services.IGitHubService</c>: it never silently updates an existing release by
/// tag name, because claim-first semantics (I3) require the caller to see "a claim already
/// exists" as a distinct outcome from "no claim yet, safe to create one" before any write.
///
/// The candidate manifest is round-tripped through the release body as a hidden HTML comment
/// (<c>manifest-json</c> marker) alongside the human-readable notes, since GitHub releases have
/// no separate structured-metadata field; the surface-manifest.json asset (S1) is the canonical,
/// durable copy.
/// </summary>
public sealed class GitHubReleaseClaimStore : IReleaseClaimStore
{
    private const string ManifestMarkerStart = "<!-- release-claim:manifest-json";
    private const string ManifestMarkerEnd = "release-claim:manifest-json -->";

    private readonly string _owner;
    private readonly string _repo;
    private readonly IGitHubClient _client;
    private readonly System.Collections.Generic.Dictionary<string, long> _createdReleaseIds = new(StringComparer.Ordinal);

    public GitHubReleaseClaimStore(string owner, string repo, string token, IGitHubClient? client = null)
    {
        _owner = owner;
        _repo = repo;
        _client = client ?? new GitHubClient(new ProductHeaderValue("NukeBuild")) { Credentials = new Credentials(token) };
    }

    public async Task<ReleaseClaim?> FindClaimAsync(ReleaseVersion version)
    {
        var releases = await ListAllAsync().ConfigureAwait(false);

        var tagName = version.ToTagString();
        var match = releases.FirstOrDefault(r => string.Equals(r.TagName, tagName, StringComparison.Ordinal));

        return match == null ? null : ToClaim(match, version);
    }

    public async Task<ReleaseClaim> CreateDraftAsync(ReleaseVersion version, string commitSha, string notes, SurfaceManifest manifest)
    {
        var body = FormatBody(notes, manifest);

        var newRelease = new NewRelease(version.ToTagString())
        {
            Name = version.ToTagString(),
            TargetCommitish = commitSha,
            Body = body,
            Draft = true,
            Prerelease = version.PreRelease != null,
        };

        try
        {
            var created = await _client.Repository.Release.Create(_owner, _repo, newRelease).ConfigureAwait(false);
            _createdReleaseIds[version.ToTagString()] = created.Id;
        }
        catch (Exception ex)
        {
            throw new ReleaseException(
                ReleaseErrorCode.ClaimCreationFailed,
                null,
                $"Could not create draft release '{version.ToTagString()}' for {_owner}/{_repo}: {ex.Message}",
                ex);
        }

        return new ReleaseClaim(version, commitSha, ClaimState.Draft, notes, manifest);
    }

    public async Task PublishAsync(ReleaseClaim claim)
    {
        var tagName = claim.Version.ToTagString();

        // Publish the exact draft this store created: drafts do not reserve a tag, so a stale draft
        // for the same tag can coexist, and a lookup by tag name could pick the wrong one.
        if (!_createdReleaseIds.TryGetValue(tagName, out var releaseId))
        {
            var releases = await ListAllAsync().ConfigureAwait(false);
            var existing = releases.FirstOrDefault(r =>
                r.Draft
                && string.Equals(r.TagName, tagName, StringComparison.Ordinal)
                && string.Equals(r.TargetCommitish, claim.CommitSha, StringComparison.Ordinal));

            if (existing == null)
            {
                throw new ReleaseException(
                    ReleaseErrorCode.ClaimCreationFailed,
                    null,
                    $"No draft release found for '{tagName}' at {claim.CommitSha} to publish.");
            }

            releaseId = existing.Id;
        }

        var update = new ReleaseUpdate { Draft = false };
        await _client.Repository.Release.Edit(_owner, _repo, releaseId, update).ConfigureAwait(false);
    }

    private async Task<System.Collections.Generic.IReadOnlyList<Octokit.Release>> ListAllAsync()
    {
        try
        {
            return await _client.Repository.Release.GetAll(_owner, _repo).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new ReleaseException(
                ReleaseErrorCode.ClaimCreationFailed,
                null,
                $"Could not list releases for {_owner}/{_repo}: {ex.Message}",
                ex);
        }
    }

    private static string FormatBody(string notes, SurfaceManifest manifest)
    {
        var manifestJson = SurfaceManifestSerializer.Serialize(manifest);
        return $"{notes}\n\n{ManifestMarkerStart}\n{manifestJson}\n{ManifestMarkerEnd}\n";
    }

    private static ReleaseClaim ToClaim(Octokit.Release release, ReleaseVersion version)
    {
        var body = release.Body ?? string.Empty;
        var notes = body;
        SurfaceManifest manifest = new(0, version.ToPackageString(), Array.Empty<SurfaceItem>());

        var startIndex = body.IndexOf(ManifestMarkerStart, StringComparison.Ordinal);
        var endIndex = body.IndexOf(ManifestMarkerEnd, StringComparison.Ordinal);
        if (startIndex >= 0 && endIndex > startIndex)
        {
            notes = body[..startIndex].TrimEnd();
            var jsonStart = startIndex + ManifestMarkerStart.Length;
            var manifestJson = body[jsonStart..endIndex].Trim();
            try
            {
                manifest = SurfaceManifestSerializer.Deserialize(manifestJson);
            }
            catch
            {
                // Leave the empty manifest placeholder; this is a diagnostic best-effort read,
                // not a write path.
            }
        }

        var state = release.Draft ? ClaimState.Draft : ClaimState.Published;
        return new ReleaseClaim(version, release.TargetCommitish ?? string.Empty, state, notes, manifest);
    }
}
