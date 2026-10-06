#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Surface;

namespace Release;

/// <summary>
/// Orchestrates a single publish attempt end to end: existence checks, the major-never-decreases
/// check and notes validation write nothing (I3); the claim is the first write; the versioned
/// sinks are then written in <see cref="ReleaseSink"/> order, the release is published, and only
/// then does <see cref="ReleaseSink.ImageLatestTag"/> move, so a release that is not published
/// never moves <c>latest</c> (I4); a sink rejecting the write fails naming that sink and leaves
/// the claim open rather than deleting anything (I5 is never undone by a failure path).
///
/// Each versioned sink's artifact identity is recorded in the claim just before that sink is
/// written. A resume compares every sink that holds the version against the claim before any
/// write, and skips a sink only on a match. A run that finds the release published for its own
/// commit is a completion re-run: it verifies the release and moves only <c>latest</c>.
///
/// <c>latest</c> names the highest published stable release: a pre-release never moves it, and
/// neither does a stable release below the highest one already published. A pre-release label is
/// one alphanumeric identifier (2.1.0-rc1), the form every sink accepts.
/// </summary>
public sealed class ReleasePipeline
{
    private readonly IReleaseClaimStore _claimStore;
    private readonly IImageTagChecker _imageTagChecker;
    private readonly IGitTagChecker _gitTagChecker;
    private readonly IHighestPublishedVersionSource _highestPublishedSource;
    private readonly ICiPublishingContext _ciContext;
    private readonly IReadOnlyList<IVersionedSinkPublisher> _versionedSinks;
    private readonly IReleaseSinkPublisher? _latestTag;

    public ReleasePipeline(
        IReleaseClaimStore claimStore,
        IImageTagChecker imageTagChecker,
        IGitTagChecker gitTagChecker,
        IHighestPublishedVersionSource highestPublishedSource,
        ICiPublishingContext ciContext,
        IEnumerable<IReleaseSinkPublisher> sinks)
    {
        _claimStore = claimStore ?? throw new ArgumentNullException(nameof(claimStore));
        _imageTagChecker = imageTagChecker ?? throw new ArgumentNullException(nameof(imageTagChecker));
        _gitTagChecker = gitTagChecker ?? throw new ArgumentNullException(nameof(gitTagChecker));
        _highestPublishedSource = highestPublishedSource ?? throw new ArgumentNullException(nameof(highestPublishedSource));
        _ciContext = ciContext ?? throw new ArgumentNullException(nameof(ciContext));
        var ordered = (sinks ?? throw new ArgumentNullException(nameof(sinks)))
            .OrderBy(sink => (int)sink.Sink)
            .ToList();

        var versioned = new List<IVersionedSinkPublisher>();
        foreach (var sink in ordered.Where(sink => sink.Sink != ReleaseSink.ImageLatestTag))
        {
            versioned.Add(sink as IVersionedSinkPublisher
                ?? throw new ArgumentException(
                    $"The {sink.Sink} publisher must be an {nameof(IVersionedSinkPublisher)}: a versioned sink reports its artifact identity.",
                    nameof(sinks)));
        }

        _versionedSinks = versioned;
        _latestTag = ordered.SingleOrDefault(sink => sink.Sink == ReleaseSink.ImageLatestTag);
    }

    public async Task<ReleaseClaim> PublishAsync(ReleaseVersion version, string commitSha, string notes, SurfaceManifest manifest)
    {
        if (version is null)
        {
            throw new ArgumentNullException(nameof(version));
        }

        if (string.IsNullOrWhiteSpace(commitSha))
        {
            throw new ArgumentException("Commit SHA cannot be empty.", nameof(commitSha));
        }

        // I14: only CI publishes. Checked first, before any other read or write.
        if (!_ciContext.IsCiPublishing)
        {
            throw new ReleaseException(
                ReleaseErrorCode.NotPublishedByCi,
                null,
                "This run is not the CI publishing context; refusing to publish.");
        }

        // The PowerShell Gallery takes a pre-release label of letters and digits only, so a label
        // every sink cannot hold is refused before anything is read or written.
        if (version.PreRelease != null && !version.PreRelease.All(char.IsAsciiLetterOrDigit))
        {
            throw new ReleaseException(
                ReleaseErrorCode.PreReleaseLabelUnsupported,
                null,
                $"Pre-release label '{version.PreRelease}' is not supported: use letters and digits only, such as {version.Major}.{version.Minor}.{version.Patch}-rc1.");
        }

        // I5 / I3: existence checking writes nothing. A claim ref or a draft for another commit, a
        // published release for another commit, a versioned image tag or a git tag on another
        // commit makes the version taken. A claim ref or a draft for this commit is a claim this
        // run resumes, a release published for this commit is one this run completes, and a git
        // tag on this commit with nothing else holding the version is a release this run makes
        // from that tag.
        var claimRefCommit = await _claimStore.FindClaimRefAsync(version).ConfigureAwait(false);
        if (claimRefCommit != null && !string.Equals(claimRefCommit, commitSha, StringComparison.Ordinal))
        {
            throw ClaimRefElsewhere(version, claimRefCommit);
        }

        var existingClaim = await _claimStore.FindClaimAsync(version).ConfigureAwait(false);
        if (existingClaim is { State: ClaimState.Published }
            && string.Equals(existingClaim.CommitSha, commitSha, StringComparison.Ordinal))
        {
            return await CompleteAsync(existingClaim).ConfigureAwait(false);
        }

        var resumedDraft = ResumableDraftOrThrow(existingClaim, version, commitSha);
        var resuming = claimRefCommit != null || resumedDraft != null;

        // A resumed claim may already have pushed the versioned image tag; the sink comparison
        // below decides whether that tag is this release's.
        if (!resuming && await _imageTagChecker.ExistsAsync(version).ConfigureAwait(false))
        {
            throw new ReleaseException(
                ReleaseErrorCode.VersionAlreadyExists,
                null,
                $"Version {version.ToPackageString()} already has a versioned image tag.");
        }

        var tagCommit = await _gitTagChecker.FindTagCommitAsync(version).ConfigureAwait(false);
        if (tagCommit != null && !string.Equals(tagCommit, commitSha, StringComparison.Ordinal))
        {
            throw new ReleaseException(
                ReleaseErrorCode.TagPointsElsewhere,
                null,
                $"Git tag {version.ToTagString()} already points at commit {tagCommit}, not {commitSha}.");
        }

        // I6: major never decreases. The highest published release is the highest stable one.
        var highestPublished = await _highestPublishedSource.GetHighestPublishedAsync().ConfigureAwait(false);
        if (highestPublished != null && version.Major < highestPublished.Major)
        {
            throw new ReleaseException(
                ReleaseErrorCode.MajorBelowCurrent,
                null,
                $"Candidate major {version.Major} is below the highest published major {highestPublished.Major}.");
        }

        // I7: notes carry both required sections.
        ReleaseNotesValidator.Validate(notes);

        foreach (var sink in _versionedSinks)
        {
            if (string.IsNullOrWhiteSpace(sink.BuiltIdentity))
            {
                throw new InvalidOperationException($"The {sink.Sink} publisher reports no artifact identity for the build.");
            }
        }

        // I5: every sink that holds the version is compared before any write. A fresh claim finds
        // none; a resume finds only sinks holding the identity its claim records.
        var held = await ReadHeldIdentitiesAsync(version).ConfigureAwait(false);
        if (!resuming)
        {
            if (held.Count > 0)
            {
                throw new ReleaseException(
                    ReleaseErrorCode.VersionAlreadyExists,
                    null,
                    $"Version {version.ToPackageString()} is not claimed, but sink {held.Keys.First()} already holds it.");
            }
        }
        else
        {
            EnsureHeldMatch(version, held, resumedDraft);
        }

        // I3 / I13: the claim is the first write of a release, and the claim ref is its first part.
        // The refs API refuses a ref that exists, so of two runs that both passed the check above
        // only one creates it; the other finds it and refuses unless it is at this run's commit.
        ClaimRef claimRef;
        try
        {
            claimRef = await _claimStore.CreateClaimRefAsync(version, commitSha).ConfigureAwait(false);
        }
        catch (ReleaseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ReleaseException(
                ReleaseErrorCode.ClaimCreationFailed,
                null,
                $"Could not create the claim ref for {version.ToPackageString()}: {ex.Message}",
                ex);
        }

        if (!string.Equals(claimRef.CommitSha, commitSha, StringComparison.Ordinal))
        {
            throw ClaimRefElsewhere(version, claimRef.CommitSha);
        }

        if (!resuming && !claimRef.Created)
        {
            // The ref appeared at this commit since the check: a run for the same commit may have
            // created the draft and written sinks since, so compare again and resume under it
            // rather than add a second draft.
            resumedDraft = ResumableDraftOrThrow(
                await _claimStore.FindClaimAsync(version).ConfigureAwait(false), version, commitSha);
            held = await ReadHeldIdentitiesAsync(version).ConfigureAwait(false);
            EnsureHeldMatch(version, held, resumedDraft);
        }

        ReleaseClaim claim;
        if (resumedDraft != null)
        {
            claim = resumedDraft;
        }
        else
        {
            try
            {
                claim = await _claimStore.CreateDraftAsync(version, commitSha, notes, manifest).ConfigureAwait(false);
            }
            catch (ReleaseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ReleaseException(
                    ReleaseErrorCode.ClaimCreationFailed,
                    null,
                    $"Could not create the draft release for {version.ToPackageString()}: {ex.Message}",
                    ex);
            }
        }

        // The versioned sinks run in ReleaseSink numeric order. ImageLatestTag is not among them.
        // A sink that holds the version matched the claim above and is skipped, so its recorded
        // identity is never replaced.
        foreach (var sink in _versionedSinks)
        {
            if (held.ContainsKey(sink.Sink))
            {
                continue;
            }

            try
            {
                claim = await _claimStore.RecordIdentityAsync(claim, sink.Sink, sink.BuiltIdentity).ConfigureAwait(false);
                await sink.PublishAsync(claim).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The claim is intentionally left open (Draft) here for a resume — no delete of
                // the claim, a tag or a package happens on any failure path (I5, I13-adjacent).
                throw new ReleaseException(
                    ReleaseErrorCode.SinkPublishFailed,
                    sink.Sink,
                    $"Sink {sink.Sink} rejected the write for {version.ToPackageString()}: {ex.Message}",
                    ex);
            }
        }

        await _claimStore.PublishAsync(claim).ConfigureAwait(false);
        var published = claim with { State = ClaimState.Published };

        if (!version.IsPreRelease && (highestPublished == null || version.CompareTo(highestPublished) >= 0))
        {
            await MoveLatestAsync(published).ConfigureAwait(false);
        }

        return published;
    }

    /// <summary>
    /// A completion re-run: the release is published for this run's commit. Before moving
    /// <c>latest</c> it checks that the git tag points at the commit, that the version is the
    /// highest published release, and that every versioned sink holds the claim's artifact.
    /// Nothing else is written. A pre-release is verified the same way, except against the
    /// highest release, and never moves <c>latest</c>.
    /// </summary>
    private async Task<ReleaseClaim> CompleteAsync(ReleaseClaim claim)
    {
        var version = claim.Version;

        var tagCommit = await _gitTagChecker.FindTagCommitAsync(version).ConfigureAwait(false);
        if (tagCommit == null)
        {
            throw new ReleaseException(
                ReleaseErrorCode.VersionAlreadyExists,
                null,
                $"Release {version.ToPackageString()} is published for {claim.CommitSha}, but git tag {version.ToTagString()} does not exist.");
        }

        if (!string.Equals(tagCommit, claim.CommitSha, StringComparison.Ordinal))
        {
            throw new ReleaseException(
                ReleaseErrorCode.TagPointsElsewhere,
                null,
                $"Git tag {version.ToTagString()} points at commit {tagCommit}, not {claim.CommitSha}.");
        }

        var highestPublished = !version.IsPreRelease
            ? await _highestPublishedSource.GetHighestPublishedAsync().ConfigureAwait(false)
            : null;
        if (!version.IsPreRelease && highestPublished != version)
        {
            var highest = highestPublished?.ToPackageString() ?? "no release";
            throw new ReleaseException(
                ReleaseErrorCode.VersionAlreadyExists,
                null,
                $"Release {version.ToPackageString()} is published, but the highest published release is {highest}; this run does not move latest back.");
        }

        var held = await ReadHeldIdentitiesAsync(version).ConfigureAwait(false);
        foreach (var sink in _versionedSinks)
        {
            if (!held.ContainsKey(sink.Sink))
            {
                throw new ReleaseException(
                    ReleaseErrorCode.SinkArtifactMismatch,
                    sink.Sink,
                    $"Sink {sink.Sink} does not hold {version.ToPackageString()}, which is published.");
            }
        }

        EnsureHeldMatch(version, held, claim);

        if (!version.IsPreRelease)
        {
            await MoveLatestAsync(claim).ConfigureAwait(false);
        }

        return claim;
    }

    /// <summary>
    /// I4: latest moves last, after the release is published. A failure here leaves a complete
    /// release; latest is movable, so nothing is undone.
    /// </summary>
    private async Task MoveLatestAsync(ReleaseClaim published)
    {
        if (_latestTag == null)
        {
            return;
        }

        try
        {
            await _latestTag.PublishAsync(published).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new ReleaseException(
                ReleaseErrorCode.SinkPublishFailed,
                ReleaseSink.ImageLatestTag,
                $"Release {published.Version.ToPackageString()} is published, but moving latest to it failed: {ex.Message}",
                ex);
        }
    }

    /// <summary>The identity each versioned sink holds for the version, for the sinks that hold it.</summary>
    private async Task<IReadOnlyDictionary<ReleaseSink, string>> ReadHeldIdentitiesAsync(ReleaseVersion version)
    {
        var held = new Dictionary<ReleaseSink, string>();
        foreach (var sink in _versionedSinks)
        {
            var identity = await sink.FindPublishedIdentityAsync(version).ConfigureAwait(false);
            if (identity != null)
            {
                held[sink.Sink] = identity;
            }
        }

        return held;
    }

    /// <summary>
    /// Every sink that holds the version must hold the identity the claim records for it. A sink
    /// with no recorded identity, including under a claim ref with no draft, does not match.
    /// </summary>
    private void EnsureHeldMatch(ReleaseVersion version, IReadOnlyDictionary<ReleaseSink, string> held, ReleaseClaim? claim)
    {
        foreach (var sink in _versionedSinks)
        {
            if (!held.TryGetValue(sink.Sink, out var heldIdentity))
            {
                continue;
            }

            var recorded = claim?.IdentityOf(sink.Sink);
            if (recorded == null)
            {
                throw new ReleaseException(
                    ReleaseErrorCode.SinkArtifactMismatch,
                    sink.Sink,
                    $"Sink {sink.Sink} holds {version.ToPackageString()} with artifact {heldIdentity}, but the claim records no identity for it.");
            }

            if (!string.Equals(recorded, heldIdentity, StringComparison.Ordinal))
            {
                throw new ReleaseException(
                    ReleaseErrorCode.SinkArtifactMismatch,
                    sink.Sink,
                    $"Sink {sink.Sink} holds {version.ToPackageString()} with artifact {heldIdentity}, not the {recorded} the claim records.");
            }
        }
    }

    private static ReleaseException ClaimRefElsewhere(ReleaseVersion version, string claimRefCommit)
    {
        return new ReleaseException(
            ReleaseErrorCode.VersionAlreadyExists,
            null,
            $"Version {version.ToPackageString()} is already claimed: claim ref {version.ToTagString()} points at commit {claimRefCommit}.");
    }

    /// <summary>
    /// A draft release for this commit is a claim this run resumes. A published release, or a
    /// draft for another commit, means the version exists (I5).
    /// </summary>
    private static ReleaseClaim? ResumableDraftOrThrow(ReleaseClaim? existingClaim, ReleaseVersion version, string commitSha)
    {
        if (existingClaim == null)
        {
            return null;
        }

        if (existingClaim.State == ClaimState.Draft
            && string.Equals(existingClaim.CommitSha, commitSha, StringComparison.Ordinal))
        {
            return existingClaim;
        }

        throw new ReleaseException(
            ReleaseErrorCode.VersionAlreadyExists,
            null,
            $"Version {version.ToPackageString()} already has a {existingClaim.State.ToString().ToLowerInvariant()} release claim bound to commit {existingClaim.CommitSha}.");
    }
}
