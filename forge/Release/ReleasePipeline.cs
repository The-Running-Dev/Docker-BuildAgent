#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Surface;

namespace Release;

/// <summary>
/// Orchestrates a single publish attempt end to end: existence checks, the major-never-decreases
/// check and notes validation write nothing (I3); the claim is the first write; every sink is
/// then written in <see cref="ReleaseSink"/> order, so <see cref="ReleaseSink.ImageLatestTag"/>
/// always moves last (I4); a sink rejecting the write fails naming that sink and leaves the claim
/// open rather than deleting anything (I5 is never undone by a failure path).
/// </summary>
public sealed class ReleasePipeline
{
    private readonly IReleaseClaimStore _claimStore;
    private readonly IImageTagChecker _imageTagChecker;
    private readonly IGitTagChecker _gitTagChecker;
    private readonly IHighestPublishedVersionSource _highestPublishedSource;
    private readonly ICiPublishingContext _ciContext;
    private readonly IReadOnlyList<IReleaseSinkPublisher> _sinks;

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
        _sinks = (sinks ?? throw new ArgumentNullException(nameof(sinks)))
            .OrderBy(sink => (int)sink.Sink)
            .ToList();
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

        // I5 / I3: existence checking writes nothing. Any one of claim, versioned image tag or
        // git tag makes the version taken.
        var existingClaim = await _claimStore.FindClaimAsync(version).ConfigureAwait(false);
        if (existingClaim != null)
        {
            throw new ReleaseException(
                ReleaseErrorCode.VersionAlreadyExists,
                null,
                $"Version {version.ToPackageString()} already has a draft release claim bound to commit {existingClaim.CommitSha}.");
        }

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

        // I3: the claim is the first write of a release.
        ReleaseClaim claim;
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

        // I4: sinks run in ReleaseSink numeric order, so ImageLatestTag is always last.
        foreach (var sink in _sinks)
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

        return claim with { State = ClaimState.Published };
    }
}
