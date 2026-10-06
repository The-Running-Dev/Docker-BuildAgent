#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using Octokit;

using Surface;

namespace Release;

/// <summary>
/// <see cref="IReleaseClaimStore"/> backed by a git ref and GitHub draft releases, via Octokit.
/// The claim ref <c>refs/release-claims/v&lt;version&gt;</c> is created through the refs API, which
/// refuses a ref that exists, so it is the atomic part of the claim (I13). Deliberately
/// narrower than <c>Services.IGitHubService</c>: it never silently updates an existing release by
/// tag name, because claim-first semantics (I3) require the caller to see "a claim already
/// exists" as a distinct outcome from "no claim yet, safe to create one" before any write.
///
/// The candidate manifest and the artifact identities are round-tripped through the release body
/// as hidden HTML comments (<c>manifest-json</c> and <c>artifact-identities</c> markers) after the
/// human-readable notes, since GitHub releases have no separate structured-metadata field; the
/// surface-manifest.json asset (S1) is the canonical, durable copy of the manifest, and the next
/// release's baseline. Publishing uploads that asset first when the draft lacks it, so no release
/// is published without it. A body whose identities cannot be read yields none, so a sink holding
/// the version does not match (fails closed).
/// </summary>
public sealed class GitHubReleaseClaimStore : IReleaseClaimStore
{
    private const string ManifestMarkerStart = "<!-- release-claim:manifest-json";
    private const string ManifestMarkerEnd = "release-claim:manifest-json -->";
    private const string IdentitiesMarkerStart = "<!-- release-claim:artifact-identities";
    private const string IdentitiesMarkerEnd = "release-claim:artifact-identities -->";

    private const string ManifestAssetName = GitHubReleaseManifestSource.ManifestAssetName;

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

    public async Task<string?> FindClaimRefAsync(ReleaseVersion version)
    {
        try
        {
            return await GetClaimRefCommitAsync(version).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new ReleaseException(
                ReleaseErrorCode.ClaimCreationFailed,
                null,
                $"Could not read claim ref '{ClaimRefName(version)}' for {_owner}/{_repo}: {ex.Message}",
                ex);
        }
    }

    public async Task<ClaimRef> CreateClaimRefAsync(ReleaseVersion version, string commitSha)
    {
        try
        {
            var created = await _client.Git.Reference
                .Create(_owner, _repo, new NewReference(ClaimRefName(version), commitSha))
                .ConfigureAwait(false);
            return new ClaimRef(created.Object?.Sha ?? commitSha, Created: true);
        }
        catch (ApiValidationException createRefused)
        {
            // The refs API refuses a ref that already exists. Read it to learn which commit holds
            // the version; any other refusal leaves the ref absent and fails the claim.
            string? existing;
            try
            {
                existing = await GetClaimRefCommitAsync(version).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new ReleaseException(
                    ReleaseErrorCode.ClaimCreationFailed,
                    null,
                    $"Could not create or read claim ref '{ClaimRefName(version)}' for {_owner}/{_repo}: {ex.Message}",
                    ex);
            }

            if (existing == null)
            {
                throw new ReleaseException(
                    ReleaseErrorCode.ClaimCreationFailed,
                    null,
                    $"Could not create claim ref '{ClaimRefName(version)}' for {_owner}/{_repo}: {createRefused.Message}",
                    createRefused);
            }

            return new ClaimRef(existing, Created: false);
        }
        catch (Exception ex)
        {
            throw new ReleaseException(
                ReleaseErrorCode.ClaimCreationFailed,
                null,
                $"Could not create claim ref '{ClaimRefName(version)}' for {_owner}/{_repo}: {ex.Message}",
                ex);
        }
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
        var body = FormatBody(notes, manifest, new System.Collections.Generic.Dictionary<ReleaseSink, string>());

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

    public async Task<ReleaseClaim> RecordIdentityAsync(ReleaseClaim claim, ReleaseSink sink, string identity)
    {
        var updated = claim.WithIdentity(sink, identity);
        var releaseId = await FindDraftIdAsync(claim, "record an artifact identity in").ConfigureAwait(false);

        var update = new ReleaseUpdate { Body = FormatBody(updated.Notes, updated.CandidateManifest, updated.ArtifactIdentities) };
        await _client.Repository.Release.Edit(_owner, _repo, releaseId, update).ConfigureAwait(false);
        return updated;
    }

    public async Task PublishAsync(ReleaseClaim claim)
    {
        var releaseId = await FindDraftIdAsync(claim, "publish").ConfigureAwait(false);
        await EnsureManifestAssetAsync(claim, releaseId).ConfigureAwait(false);

        var update = new ReleaseUpdate { Draft = false };
        await _client.Repository.Release.Edit(_owner, _repo, releaseId, update).ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads the claim's candidate manifest as <see cref="ManifestAssetName"/> unless the draft
    /// already carries it, as a resumed draft may. A claim whose manifest could not be read back
    /// from the draft is refused rather than published with a placeholder baseline.
    /// </summary>
    private async Task EnsureManifestAssetAsync(ReleaseClaim claim, long releaseId)
    {
        var release = await _client.Repository.Release.Get(_owner, _repo, releaseId).ConfigureAwait(false);
        if (release.Assets?.Any(a => string.Equals(a.Name, ManifestAssetName, StringComparison.Ordinal)) == true)
        {
            return;
        }

        if (claim.CandidateManifest.ManifestSchemaVersion < 1)
        {
            throw new ReleaseException(
                ReleaseErrorCode.ClaimCreationFailed,
                null,
                $"The draft for '{claim.Version.ToTagString()}' has no readable surface manifest to publish as {ManifestAssetName}.");
        }

        var json = SurfaceManifestSerializer.Serialize(claim.CandidateManifest);
        using var stream = new System.IO.MemoryStream(SurfaceManifestSerializer.Utf8NoBom.GetBytes(json));
        await _client.Repository.Release
            .UploadAsset(release, new ReleaseAssetUpload(ManifestAssetName, "application/json", stream, null))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The exact draft this store created, or else the draft for the claim's tag at its commit:
    /// drafts do not reserve a tag, so a stale draft for the same tag can coexist, and a lookup by
    /// tag name alone could pick the wrong one.
    /// </summary>
    private async Task<long> FindDraftIdAsync(ReleaseClaim claim, string purpose)
    {
        var tagName = claim.Version.ToTagString();
        if (_createdReleaseIds.TryGetValue(tagName, out var releaseId))
        {
            return releaseId;
        }

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
                $"No draft release found for '{tagName}' at {claim.CommitSha} to {purpose}.");
        }

        return existing.Id;
    }

    /// <summary>The fully qualified claim ref for a version: <c>refs/release-claims/v&lt;version&gt;</c>.</summary>
    public static string ClaimRefName(ReleaseVersion version) => $"refs/{ClaimRefNamespace}/{version.ToTagString()}";

    private const string ClaimRefNamespace = "release-claims";

    private async Task<string?> GetClaimRefCommitAsync(ReleaseVersion version)
    {
        // Listed and matched exactly: a single-ref read by name falls back to prefix matching when
        // the ref is absent, so v2.0.0 could otherwise answer with v2.0.0-rc.1's ref.
        System.Collections.Generic.IReadOnlyList<Reference> references;
        try
        {
            references = await _client.Git.Reference.GetAllForSubNamespace(_owner, _repo, ClaimRefNamespace).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            return null;
        }

        var name = ClaimRefName(version);
        return references?.FirstOrDefault(r => string.Equals(r.Ref, name, StringComparison.Ordinal))?.Object?.Sha;
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

    private static string FormatBody(string notes, SurfaceManifest manifest, System.Collections.Generic.IReadOnlyDictionary<ReleaseSink, string> identities)
    {
        var manifestJson = SurfaceManifestSerializer.Serialize(manifest);
        var identitiesJson = System.Text.Json.JsonSerializer.Serialize(
            identities.OrderBy(pair => (int)pair.Key).ToDictionary(pair => pair.Key.ToString(), pair => pair.Value));
        return $"{notes}\n\n{ManifestMarkerStart}\n{manifestJson}\n{ManifestMarkerEnd}\n\n{IdentitiesMarkerStart}\n{identitiesJson}\n{IdentitiesMarkerEnd}\n";
    }

    private static System.Collections.Generic.IReadOnlyDictionary<ReleaseSink, string> ParseIdentities(string body)
    {
        var identities = new System.Collections.Generic.Dictionary<ReleaseSink, string>();
        var startIndex = body.IndexOf(IdentitiesMarkerStart, StringComparison.Ordinal);
        var endIndex = body.IndexOf(IdentitiesMarkerEnd, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex <= startIndex)
        {
            return identities;
        }

        var json = body[(startIndex + IdentitiesMarkerStart.Length)..endIndex].Trim();
        System.Collections.Generic.Dictionary<string, string>? recorded;
        try
        {
            recorded = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return identities;
        }

        foreach (var (name, identity) in recorded ?? new())
        {
            if (Enum.TryParse<ReleaseSink>(name, ignoreCase: false, out var sink)
                && Enum.IsDefined(sink)
                && !string.IsNullOrWhiteSpace(identity))
            {
                identities[sink] = identity;
            }
        }

        return identities;
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
        return new ReleaseClaim(version, release.TargetCommitish ?? string.Empty, state, notes, manifest)
        {
            ArtifactIdentities = ParseIdentities(body),
        };
    }
}
