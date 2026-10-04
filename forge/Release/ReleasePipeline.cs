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
/// </summary>
public sealed class ReleasePipeline
{
    private readonly IReleaseClaimStore _claimStore;
    private readonly IImageTagChecker _imageTagChecker;
    private readonly IGitTagChecker _gitTagChecker;
    private readonly IHighestPublishedVersionSource _highestPublishedSource;
    private readonly ICiPublishingContext _ciContext;
    private readonly IReadOnlyList<IReleaseSinkPublisher> _versionedSinks;
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
        _versionedSinks = ordered.Where(sink => sink.Sink != ReleaseSink.ImageLatestTag).ToList();
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

        // I5 / I3: existence checking writes nothing. A claim ref or a draft for another commit, a
        // published release, a versioned image tag or a git tag makes the version taken. A claim
        // ref or a draft for this commit is a claim this run resumes.
        var claimRefCommit = await _claimStore.FindClaimRefAsync(version).ConfigureAwait(false);
        if (claimRefCommit != null && !string.Equals(claimRefCommit, commitSha, StringComparison.Ordinal))
        {
            throw ClaimRefElsewhere(version, claimRefCommit);
        }

        var existingClaim = await _claimStore.FindClaimAsync(version).ConfigureAwait(false);
        var resumedDraft = ResumableDraftOrThrow(existingClaim, version, commitSha);

        if (await _imageTagChecker.ExistsAsync(version).ConfigureAwait(false))
        {
            throw new ReleaseException(
                ReleaseErrorCode.VersionAlreadyExists,
                null,
                $"Version {version.ToPackageString()} already has a versioned image tag.");
        }

        var tagCommit = await _gitTagChecker.FindTagCommitAsync(version).ConfigureAwait(false);
        if (tagCommit != null)
        {
            if (string.Equals(tagCommit, commitSha, StringComparison.Ordinal))
            {
                throw new ReleaseException(
                    ReleaseErrorCode.VersionAlreadyExists,
                    null,
                    $"Version {version.ToPackageString()} already has git tag {version.ToTagString()}.");
            }

            throw new ReleaseException(
                ReleaseErrorCode.TagPointsElsewhere,
                null,
                $"Git tag {version.ToTagString()} already points at commit {tagCommit}, not {commitSha}.");
        }

        // I6: major never decreases.
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

        if (resumedDraft == null && !claimRef.Created)
        {
            // The ref was already there at this commit: a run for the same commit may have created
            // the draft since the check, so resume under it rather than add a second one.
            resumedDraft = ResumableDraftOrThrow(
                await _claimStore.FindClaimAsync(version).ConfigureAwait(false), version, commitSha);
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
        foreach (var sink in _versionedSinks)
        {
            try
            {
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

        // I4: latest moves last, after the release is published. A failure here leaves a complete
        // release; latest is movable, so nothing is undone.
        if (_latestTag != null)
        {
            try
            {
                await _latestTag.PublishAsync(published).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new ReleaseException(
                    ReleaseErrorCode.SinkPublishFailed,
                    ReleaseSink.ImageLatestTag,
                    $"Release {version.ToPackageString()} is published, but moving latest to it failed: {ex.Message}",
                    ex);
            }
        }

        return published;
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
