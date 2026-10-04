using System;
using System.Linq;
using System.Threading.Tasks;

using Release.Tests.TestSupport;

using Xunit;

namespace Release.Tests;

public sealed class ReleasePipelineTests
{
    private static readonly ReleaseVersion Version = new(2, 0, 0, null);
    private const string CommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherCommitSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static (
        ReleasePipeline Pipeline,
        FakeClaimStore ClaimStore,
        FakeImageTagChecker ImageTagChecker,
        FakeGitTagChecker GitTagChecker,
        FakeHighestPublishedVersionSource HighestPublished,
        FakeCiPublishingContext CiContext,
        FakeSinkPublisher[] Sinks) Build()
    {
        var claimStore = new FakeClaimStore();
        var imageTagChecker = new FakeImageTagChecker();
        var gitTagChecker = new FakeGitTagChecker();
        var highestPublished = new FakeHighestPublishedVersionSource();
        var ciContext = new FakeCiPublishingContext();

        // Deliberately constructed out of publish order (I4) to prove the pipeline sorts them
        // itself rather than relying on registration order.
        var sinks = new[]
        {
            new FakeSinkPublisher(ReleaseSink.ImageLatestTag),
            new FakeSinkPublisher(ReleaseSink.PowerShellModule),
            new FakeSinkPublisher(ReleaseSink.ImageVersionedTag),
            new FakeSinkPublisher(ReleaseSink.GlobalTool),
        };

        var pipeline = new ReleasePipeline(claimStore, imageTagChecker, gitTagChecker, highestPublished, ciContext, sinks);

        return (pipeline, claimStore, imageTagChecker, gitTagChecker, highestPublished, ciContext, sinks);
    }

    // S3.1: A version already held by a claim, a versioned image tag or a git tag fails with
    // VersionAlreadyExists before any write, naming which of the three held it (I5).
    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenClaimAlreadyHoldsVersion()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.Seed(new ReleaseClaim(Version, CommitSha, ClaimState.Draft, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains(CommitSha, ex.Message);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenImageTagAlreadyExists()
    {
        var (pipeline, _, imageTagChecker, _, _, _, sinks) = Build();
        imageTagChecker.SeedExisting(Version);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains("image tag", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenGitTagAlreadyPointsAtSameCommit()
    {
        var (pipeline, _, _, gitTagChecker, _, _, sinks) = Build();
        gitTagChecker.Seed(Version, CommitSha);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains("git tag", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // S3.2: A draft claim bound to a different commit fails with VersionAlreadyExists, naming
    // that commit.
    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_NamingTheOtherCommit_WhenClaimBoundElsewhere()
    {
        var (pipeline, claimStore, _, _, _, _, _) = Build();
        claimStore.Seed(new ReleaseClaim(Version, OtherCommitSha, ClaimState.Draft, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains(OtherCommitSha, ex.Message);
    }

    // S3.3: A candidate major below the highest published major fails with MajorBelowCurrent
    // before any write.
    [Fact]
    public async Task Publish_FailsWithMajorBelowCurrent_WhenCandidateMajorIsLower()
    {
        var (pipeline, claimStore, _, _, highestPublished, _, sinks) = Build();
        highestPublished.Highest = new ReleaseVersion(3, 0, 0, null);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.MajorBelowCurrent, ex.Code);
        Assert.Empty(claimStore.Claims);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // S3.4: A git tag for the version on another commit fails with TagPointsElsewhere.
    [Fact]
    public async Task Publish_FailsWithTagPointsElsewhere_WhenGitTagPointsAtAnotherCommit()
    {
        var (pipeline, claimStore, _, gitTagChecker, _, _, sinks) = Build();
        gitTagChecker.Seed(Version, OtherCommitSha);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.TagPointsElsewhere, ex.Code);
        Assert.Contains(OtherCommitSha, ex.Message);
        Assert.Empty(claimStore.Claims);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // S3.5: Release notes lacking either the breaking-changes or the deprecations heading fail
    // with NotesSectionMissing before any write. An empty section under a present heading passes
    // (I7).
    [Theory]
    [InlineData(nameof(TestNotes.MissingBreakingChanges))]
    [InlineData(nameof(TestNotes.MissingDeprecations))]
    [InlineData(nameof(TestNotes.MissingBoth))]
    [InlineData(nameof(TestNotes.IssueReferencesAreNotHeadings))]
    public async Task Publish_FailsWithNotesSectionMissing_WhenARequiredHeadingIsAbsent(string notesFieldName)
    {
        var notes = notesFieldName switch
        {
            nameof(TestNotes.MissingBreakingChanges) => TestNotes.MissingBreakingChanges,
            nameof(TestNotes.MissingDeprecations) => TestNotes.MissingDeprecations,
            nameof(TestNotes.MissingBoth) => TestNotes.MissingBoth,
            nameof(TestNotes.IssueReferencesAreNotHeadings) => TestNotes.IssueReferencesAreNotHeadings,
            _ => throw new ArgumentOutOfRangeException(nameof(notesFieldName)),
        };

        var (pipeline, claimStore, _, _, _, _, sinks) = Build();

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, notes, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.NotesSectionMissing, ex.Code);
        Assert.Empty(claimStore.Claims);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    [Fact]
    public async Task Publish_Succeeds_WhenRequiredHeadingsArePresentButEmpty()
    {
        var (pipeline, claimStore, _, _, _, _, _) = Build();

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.EmptySectionsPass, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(ClaimState.Published, claim.State);
        Assert.Single(claimStore.Claims);
    }

    // S3.6: The claim is the first write of a release: with every publish step forced to fail,
    // the draft claim exists and no sink holds the version (I3).
    [Fact]
    public async Task Publish_LeavesDraftClaim_WhenEverySinkFails()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        foreach (var sink in sinks)
        {
            sink.ShouldFail = true;
        }

        await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        var claim = Assert.Single(claimStore.Claims).Value;
        Assert.Equal(ClaimState.Draft, claim.State);
        Assert.Equal(Version, claim.Version);
    }

    // S3.7: The version is computed once and stamped into every artifact the release produces,
    // which carry byte-identical version strings (I1).
    [Fact]
    public async Task Publish_StampsTheSamePackageVersionString_IntoEverySink()
    {
        var (pipeline, _, _, _, _, _, sinks) = Build();

        await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.All(sinks, s => Assert.True(s.WasCalled));
        var distinctVersionStrings = sinks.Select(s => s.RecordedPackageVersion).Distinct();
        Assert.Single(distinctVersionStrings);
        Assert.Equal(Version.ToPackageString(), sinks[0].RecordedPackageVersion);
    }

    // S3.10: A publish attempted outside the CI publishing context fails with NotPublishedByCi
    // and writes nothing (I14).
    [Fact]
    public async Task Publish_FailsWithNotPublishedByCi_WhenNotRunningInCi()
    {
        var (pipeline, claimStore, _, _, _, ciContext, sinks) = Build();
        ciContext.IsCiPublishing = false;

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.NotPublishedByCi, ex.Code);
        Assert.Empty(claimStore.Claims);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // S3.13: No failure path deletes a tag, a package or a release.
    // FakeClaimStore exposes no delete/remove member at all (IReleaseClaimStore has none), so this
    // is a direct exercise of every failure path against a claim seeded up front, proving none of
    // them removes it.
    [Theory]
    [MemberData(nameof(FailurePaths))]
    public async Task Publish_NeverRemovesAnExistingClaim_OnAnyFailurePath(Action<FakeClaimStore, FakeImageTagChecker, FakeGitTagChecker, FakeHighestPublishedVersionSource, FakeCiPublishingContext, FakeSinkPublisher[]> arrange)
    {
        var (pipeline, claimStore, imageTagChecker, gitTagChecker, highestPublished, ciContext, sinks) = Build();

        // Seed one already-published claim for a different version so it can't itself trigger
        // VersionAlreadyExists, then attempt a failing publish of a different version and confirm
        // the seeded claim survives untouched.
        var sentinelVersion = new ReleaseVersion(1, 0, 0, null);
        var sentinelClaim = new ReleaseClaim(sentinelVersion, CommitSha, ClaimState.Published, TestNotes.Valid, TestManifest.Empty(sentinelVersion.ToPackageString()));
        claimStore.Seed(sentinelClaim);

        arrange(claimStore, imageTagChecker, gitTagChecker, highestPublished, ciContext, sinks);

        try
        {
            await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));
        }
        catch (ReleaseException)
        {
            // Expected for every arrangement in this theory.
        }

        Assert.True(claimStore.Claims.ContainsKey(sentinelVersion.ToTagString()));
        Assert.Equal(ClaimState.Published, claimStore.Claims[sentinelVersion.ToTagString()].State);
    }

    public static TheoryData<Action<FakeClaimStore, FakeImageTagChecker, FakeGitTagChecker, FakeHighestPublishedVersionSource, FakeCiPublishingContext, FakeSinkPublisher[]>> FailurePaths()
    {
        return new()
        {
            (_, _, _, _, ci, _) => ci.IsCiPublishing = false,
            (_, imageTagChecker, _, _, _, _) => imageTagChecker.SeedExisting(Version),
            (_, _, gitTagChecker, _, _, _) => gitTagChecker.Seed(Version, OtherCommitSha),
            (_, _, _, highestPublished, _, _) => highestPublished.Highest = new ReleaseVersion(99, 0, 0, null),
            (claimStore, _, _, _, _, _) => claimStore.FailCreateDraft = (_, _, _, _) => new InvalidOperationException("boom"),
            (_, _, _, _, _, sinks) => sinks[0].ShouldFail = true,
        };
    }

    // S3.14: A sink that rejects a write fails with SinkPublishFailed naming the sink, and leaves
    // the claim open rather than deleting it.
    [Fact]
    public async Task Publish_FailsWithSinkPublishFailed_NamingTheSink_AndLeavesClaimOpen()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        var failingSink = Array.Find(sinks, s => s.Sink == ReleaseSink.GlobalTool)!;
        failingSink.ShouldFail = true;

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkPublishFailed, ex.Code);
        Assert.Equal(ReleaseSink.GlobalTool, ex.Sink);

        var claim = Assert.Single(claimStore.Claims).Value;
        Assert.Equal(ClaimState.Draft, claim.State);
    }

    // Ordering guard for I4: the versioned sinks run in ReleaseSink numeric order (ImageVersionedTag,
    // GlobalTool, PowerShellModule) regardless of registration order, then the release is
    // published, and only then does ImageLatestTag move, as the last write.
    [Fact]
    public async Task Publish_RunsVersionedSinksInOrder_ThenPublishesTheRelease_ThenMovesLatest()
    {
        var callOrder = new System.Collections.Generic.List<string>();
        var claimStore = new FakeClaimStore { OnPublished = _ => callOrder.Add("ReleasePublished") };
        var imageTagChecker = new FakeImageTagChecker();
        var gitTagChecker = new FakeGitTagChecker();
        var highestPublished = new FakeHighestPublishedVersionSource();
        var ciContext = new FakeCiPublishingContext();

        var sinks = new[]
        {
            new OrderRecordingSink(ReleaseSink.ImageLatestTag, callOrder),
            new OrderRecordingSink(ReleaseSink.GlobalTool, callOrder),
            new OrderRecordingSink(ReleaseSink.ImageVersionedTag, callOrder),
            new OrderRecordingSink(ReleaseSink.PowerShellModule, callOrder),
        };

        var pipeline = new ReleasePipeline(claimStore, imageTagChecker, gitTagChecker, highestPublished, ciContext, sinks);

        await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(
            new[]
            {
                nameof(ReleaseSink.ImageVersionedTag),
                nameof(ReleaseSink.GlobalTool),
                nameof(ReleaseSink.PowerShellModule),
                "ReleasePublished",
                nameof(ReleaseSink.ImageLatestTag),
            },
            callOrder);
    }

    // I4: a release that is not published never moves latest. Publishing the release fails after
    // every versioned sink is written; latest stays where it was and the claim stays a draft.
    [Fact]
    public async Task Publish_LeavesLatestUnmoved_WhenPublishingTheReleaseFails()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.FailPublish = new InvalidOperationException("release publish rejected");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.All(
            sinks.Where(s => s.Sink != ReleaseSink.ImageLatestTag),
            s => Assert.True(s.WasCalled));
        Assert.False(Array.Find(sinks, s => s.Sink == ReleaseSink.ImageLatestTag)!.WasCalled);
        Assert.Equal(ClaimState.Draft, Assert.Single(claimStore.Claims).Value.State);
    }

    // A versioned sink failing stops before the release is published, so latest is not moved.
    [Fact]
    public async Task Publish_LeavesLatestUnmoved_WhenAVersionedSinkFails()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        Array.Find(sinks, s => s.Sink == ReleaseSink.PowerShellModule)!.ShouldFail = true;

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseSink.PowerShellModule, ex.Sink);
        Assert.False(Array.Find(sinks, s => s.Sink == ReleaseSink.ImageLatestTag)!.WasCalled);
        Assert.Equal(ClaimState.Draft, Assert.Single(claimStore.Claims).Value.State);
    }

    // A failure to move latest after the release is published fails as SinkPublishFailed naming
    // ImageLatestTag, and leaves a complete, published release behind (20-contract.md).
    [Fact]
    public async Task Publish_FailsWithSinkPublishFailed_NamingImageLatestTag_AndLeavesTheReleasePublished()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        Array.Find(sinks, s => s.Sink == ReleaseSink.ImageLatestTag)!.ShouldFail = true;

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkPublishFailed, ex.Code);
        Assert.Equal(ReleaseSink.ImageLatestTag, ex.Sink);
        Assert.Equal(ClaimState.Published, Assert.Single(claimStore.Claims).Value.State);
    }

    // The latest publisher is handed the published claim.
    [Fact]
    public async Task Publish_HandsTheLatestPublisherAPublishedClaim()
    {
        var states = new System.Collections.Generic.List<ClaimState>();
        var pipeline = new ReleasePipeline(
            new FakeClaimStore(),
            new FakeImageTagChecker(),
            new FakeGitTagChecker(),
            new FakeHighestPublishedVersionSource(),
            new FakeCiPublishingContext(),
            new IReleaseSinkPublisher[] { new StateRecordingSink(ReleaseSink.ImageLatestTag, states) });

        await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(new[] { ClaimState.Published }, states);
    }

    private sealed class OrderRecordingSink : IReleaseSinkPublisher
    {
        private readonly System.Collections.Generic.List<string> _callOrder;

        public OrderRecordingSink(ReleaseSink sink, System.Collections.Generic.List<string> callOrder)
        {
            Sink = sink;
            _callOrder = callOrder;
        }

        public ReleaseSink Sink { get; }

        public Task PublishAsync(ReleaseClaim claim)
        {
            _callOrder.Add(Sink.ToString());
            return Task.CompletedTask;
        }
    }

    private sealed class StateRecordingSink : IReleaseSinkPublisher
    {
        private readonly System.Collections.Generic.List<ClaimState> _states;

        public StateRecordingSink(ReleaseSink sink, System.Collections.Generic.List<ClaimState> states)
        {
            Sink = sink;
            _states = states;
        }

        public ReleaseSink Sink { get; }

        public Task PublishAsync(ReleaseClaim claim)
        {
            _states.Add(claim.State);
            return Task.CompletedTask;
        }
    }
}
