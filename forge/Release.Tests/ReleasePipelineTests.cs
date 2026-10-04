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

    private static FakeSinkPublisher SinkFor(FakeSinkPublisher[] sinks, ReleaseSink sink) => Array.Find(sinks, s => s.Sink == sink)!;

    private static ReleaseClaim Claim(string commitSha, ClaimState state) =>
        new(Version, commitSha, state, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

    /// <summary>A release published for this commit, every versioned sink holding its recorded artifact.</summary>
    private static ReleaseClaim SeedCompleteRelease(FakeClaimStore claimStore, FakeGitTagChecker gitTagChecker, FakeHighestPublishedVersionSource highestPublished, FakeSinkPublisher[] sinks)
    {
        var claim = Claim(CommitSha, ClaimState.Published);
        foreach (var sink in sinks.Where(s => s.Sink != ReleaseSink.ImageLatestTag))
        {
            claim = claim.WithIdentity(sink.Sink, sink.BuiltIdentity);
            sink.HeldIdentity = sink.BuiltIdentity;
        }

        claimStore.SeedRef(Version, CommitSha);
        claimStore.Seed(claim);
        gitTagChecker.Seed(Version, CommitSha);
        highestPublished.Highest = Version;
        return claim;
    }

    // S3.1: A version already held by a claim, a versioned image tag or a git tag fails with
    // VersionAlreadyExists before any write, naming which of the three held it (I5). A release
    // published for this run's own commit is a completion re-run instead, covered below.
    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenClaimAlreadyHoldsVersion()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.Seed(Claim(OtherCommitSha, ClaimState.Published));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains(OtherCommitSha, ex.Message);
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
            (claimStore, _, _, _, _, _) => claimStore.FailCreateRef = new InvalidOperationException("boom"),
            (claimStore, _, _, _, _, _) => claimStore.SeedRef(Version, OtherCommitSha),
            (claimStore, _, _, _, _, _) => claimStore.BeforeCreateRef = store => store.SeedRef(Version, OtherCommitSha),
            (_, _, _, _, _, sinks) => sinks[0].ShouldFail = true,
            (_, _, _, _, _, sinks) => sinks[2].ShouldFail = true,
            (claimStore, _, _, _, _, _) => claimStore.FailRecordIdentity = new InvalidOperationException("boom"),
            (_, _, _, _, _, sinks) => sinks[1].HeldIdentity = "sha256:someone-else",
            (claimStore, _, _, _, _, sinks) =>
            {
                claimStore.SeedRef(Version, CommitSha);
                sinks[2].HeldIdentity = "sha256:unrecorded";
            },
            (claimStore, _, _, _, _, sinks) =>
            {
                claimStore.Seed(Claim(CommitSha, ClaimState.Draft).WithIdentity(sinks[3].Sink, "sha256:recorded"));
                sinks[3].HeldIdentity = "sha256:different";
            },
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

    // I3 / I13: the claim ref is the first write, before the draft release.
    [Fact]
    public async Task Publish_CreatesTheClaimRefAtTheCommit_BeforeTheDraft()
    {
        var (pipeline, claimStore, _, _, _, _, _) = Build();
        string? refWhenDraftCreated = null;
        claimStore.FailCreateDraft = (version, _, _, _) =>
        {
            refWhenDraftCreated = claimStore.Refs.TryGetValue(version.ToTagString(), out var sha) ? sha : null;
            return null;
        };

        await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(CommitSha, refWhenDraftCreated);
        Assert.Equal(CommitSha, claimStore.Refs[Version.ToTagString()]);
    }

    // A claim ref at another commit means another run holds the version: refuse before any write.
    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenTheClaimRefIsAtAnotherCommit()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.SeedRef(Version, OtherCommitSha);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains(OtherCommitSha, ex.Message);
        Assert.Equal(0, claimStore.DraftsCreated);
        Assert.Equal(OtherCommitSha, claimStore.Refs[Version.ToTagString()]);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // A claim ref at this commit with no draft is a resume: the run creates the draft under it.
    [Fact]
    public async Task Publish_ResumesUnderAClaimRefAtTheSameCommit_CreatingTheMissingDraft()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.SeedRef(Version, CommitSha);

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(ClaimState.Published, claim.State);
        Assert.Equal(1, claimStore.DraftsCreated);
        Assert.Equal(CommitSha, claimStore.Refs[Version.ToTagString()]);
        Assert.All(sinks, s => Assert.True(s.WasCalled));
    }

    // A claim ref and a draft at this commit are a resume under the existing draft: no second draft.
    [Fact]
    public async Task Publish_ResumesUnderTheExistingDraft_WhenTheClaimRefAndDraftAreAtTheSameCommit()
    {
        var (pipeline, claimStore, _, _, _, _, _) = Build();
        claimStore.SeedRef(Version, CommitSha);
        claimStore.Seed(new ReleaseClaim(Version, CommitSha, ClaimState.Draft, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(ClaimState.Published, claim.State);
        Assert.Equal(0, claimStore.DraftsCreated);
        Assert.Equal(ClaimState.Published, Assert.Single(claimStore.Claims).Value.State);
    }

    // The race: both runs pass the check, and the other run creates the claim ref at its commit
    // first. The refs API refuses this run's ref; this run reads it and refuses, writing nothing.
    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenAnotherRunClaimsTheVersionFirst()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.BeforeCreateRef = store => store.SeedRef(Version, OtherCommitSha);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains(OtherCommitSha, ex.Message);
        Assert.Equal(0, claimStore.DraftsCreated);
        Assert.Empty(claimStore.Claims);
        Assert.Equal(OtherCommitSha, claimStore.Refs[Version.ToTagString()]);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // The race for the same commit: the other run created the ref and the draft first. This run
    // resumes under that draft instead of adding a second one.
    [Fact]
    public async Task Publish_ResumesUnderTheOtherRunsDraft_WhenARunForTheSameCommitClaimsFirst()
    {
        var (pipeline, claimStore, _, _, _, _, _) = Build();
        claimStore.BeforeCreateRef = store =>
        {
            store.SeedRef(Version, CommitSha);
            store.Seed(new ReleaseClaim(Version, CommitSha, ClaimState.Draft, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));
        };

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(ClaimState.Published, claim.State);
        Assert.Equal(0, claimStore.DraftsCreated);
    }

    // The race when the other run, for another commit, created a draft but a ref at this commit
    // exists: the draft for another commit still means the version exists.
    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenADraftForAnotherCommitAppearsUnderTheRef()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.BeforeCreateRef = store =>
        {
            store.SeedRef(Version, CommitSha);
            store.Seed(new ReleaseClaim(Version, OtherCommitSha, ClaimState.Draft, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));
        };

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains(OtherCommitSha, ex.Message);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // A claim ref that cannot be created fails as ClaimCreationFailed, and no draft is created.
    [Fact]
    public async Task Publish_FailsWithClaimCreationFailed_WhenTheClaimRefCannotBeCreated()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.FailCreateRef = new InvalidOperationException("refs API unavailable");

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.ClaimCreationFailed, ex.Code);
        Assert.Equal(0, claimStore.DraftsCreated);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
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

    // A versioned sink must report its artifact identity, so the pipeline refuses one that cannot.
    [Fact]
    public void Constructor_RejectsAVersionedSinkThatReportsNoIdentity()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ReleasePipeline(
            new FakeClaimStore(),
            new FakeImageTagChecker(),
            new FakeGitTagChecker(),
            new FakeHighestPublishedVersionSource(),
            new FakeCiPublishingContext(),
            new IReleaseSinkPublisher[] { new StateRecordingSink(ReleaseSink.GlobalTool, new()) }));

        Assert.Contains(nameof(ReleaseSink.GlobalTool), ex.Message);
    }

    // F4: each versioned sink's identity is recorded in the claim just before that sink is
    // written; latest records none.
    [Fact]
    public async Task Publish_RecordsEachSinksIdentity_JustBeforeWritingIt()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(
            new[] { ReleaseSink.ImageVersionedTag, ReleaseSink.GlobalTool, ReleaseSink.PowerShellModule },
            claimStore.IdentitiesRecorded);
        foreach (var sink in sinks.Where(s => s.Sink != ReleaseSink.ImageLatestTag))
        {
            Assert.Equal(sink.BuiltIdentity, sink.IdentityRecordedAtWrite);
            Assert.Equal(sink.BuiltIdentity, claim.IdentityOf(sink.Sink));
        }

        Assert.Null(claim.IdentityOf(ReleaseSink.ImageLatestTag));
        Assert.Equal(3, claimStore.Claims[Version.ToTagString()].ArtifactIdentities.Count);
    }

    // F4: a resume skips a sink that holds the version with the identity the claim records, and
    // leaves that identity as it was; the unwritten sinks are written.
    [Fact]
    public async Task Publish_ResumeSkipsASinkHoldingTheClaimsArtifact_AndKeepsItsIdentity()
    {
        var (pipeline, claimStore, imageTagChecker, _, _, _, sinks) = Build();
        var image = SinkFor(sinks, ReleaseSink.ImageVersionedTag);
        image.HeldIdentity = "sha256:first-run";
        image.BuiltIdentity = "sha256:rebuilt";
        imageTagChecker.SeedExisting(Version);
        claimStore.SeedRef(Version, CommitSha);
        claimStore.Seed(Claim(CommitSha, ClaimState.Draft).WithIdentity(ReleaseSink.ImageVersionedTag, "sha256:first-run"));

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(ClaimState.Published, claim.State);
        Assert.False(image.WasCalled);
        Assert.Equal("sha256:first-run", claim.IdentityOf(ReleaseSink.ImageVersionedTag));
        Assert.DoesNotContain(ReleaseSink.ImageVersionedTag, claimStore.IdentitiesRecorded);
        Assert.True(SinkFor(sinks, ReleaseSink.GlobalTool).WasCalled);
        Assert.True(SinkFor(sinks, ReleaseSink.PowerShellModule).WasCalled);
        Assert.True(SinkFor(sinks, ReleaseSink.ImageLatestTag).WasCalled);
    }

    // F4: a sink holding a different artifact than the claim records fails naming that sink, and
    // nothing is written or recorded.
    [Fact]
    public async Task Publish_FailsWithSinkArtifactMismatch_WhenAHeldSinkDiffersFromTheClaim()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        SinkFor(sinks, ReleaseSink.GlobalTool).HeldIdentity = "sha256:other-build";
        claimStore.SeedRef(Version, CommitSha);
        claimStore.Seed(Claim(CommitSha, ClaimState.Draft).WithIdentity(ReleaseSink.GlobalTool, "sha256:recorded"));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkArtifactMismatch, ex.Code);
        Assert.Equal(ReleaseSink.GlobalTool, ex.Sink);
        Assert.Contains("sha256:other-build", ex.Message);
        Assert.Contains("sha256:recorded", ex.Message);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
        Assert.Empty(claimStore.IdentitiesRecorded);
        Assert.Equal(ClaimState.Draft, Assert.Single(claimStore.Claims).Value.State);
    }

    // F4: a sink holding the version with no identity recorded for it does not match.
    [Fact]
    public async Task Publish_FailsWithSinkArtifactMismatch_WhenTheClaimRecordsNoIdentityForAHeldSink()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        SinkFor(sinks, ReleaseSink.PowerShellModule).HeldIdentity = "sha256:unknown";
        claimStore.Seed(Claim(CommitSha, ClaimState.Draft));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkArtifactMismatch, ex.Code);
        Assert.Equal(ReleaseSink.PowerShellModule, ex.Sink);
        Assert.Contains("records no identity", ex.Message);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
        Assert.Empty(claimStore.IdentitiesRecorded);
    }

    // F4: under a claim ref with no draft there is no recorded identity, so a held sink fails and
    // no draft is created.
    [Fact]
    public async Task Publish_FailsWithSinkArtifactMismatch_WhenASinkIsHeldUnderAClaimRefWithNoDraft()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        SinkFor(sinks, ReleaseSink.ImageVersionedTag).HeldIdentity = "sha256:orphan";
        claimStore.SeedRef(Version, CommitSha);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkArtifactMismatch, ex.Code);
        Assert.Equal(ReleaseSink.ImageVersionedTag, ex.Sink);
        Assert.Equal(0, claimStore.DraftsCreated);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // F4: every held sink is compared before any write, so a mismatch on a later sink is found
    // before the first sink is written.
    [Fact]
    public async Task Publish_FindsALaterSinksMismatch_BeforeWritingTheFirstSink()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        SinkFor(sinks, ReleaseSink.PowerShellModule).HeldIdentity = "sha256:other-build";
        claimStore.SeedRef(Version, CommitSha);
        claimStore.Seed(Claim(CommitSha, ClaimState.Draft).WithIdentity(ReleaseSink.PowerShellModule, "sha256:recorded"));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseSink.PowerShellModule, ex.Sink);
        Assert.False(SinkFor(sinks, ReleaseSink.ImageVersionedTag).WasCalled);
        Assert.False(SinkFor(sinks, ReleaseSink.GlobalTool).WasCalled);
    }

    // F4: a recorded identity for a sink that does not hold the version is replaced with this
    // build's identity before the sink is written.
    [Fact]
    public async Task Publish_RecordsThisBuildsIdentity_ForAnUnwrittenSinkWithAnOldRecord()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.SeedRef(Version, CommitSha);
        claimStore.Seed(Claim(CommitSha, ClaimState.Draft).WithIdentity(ReleaseSink.GlobalTool, "sha256:failed-write"));

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        var tool = SinkFor(sinks, ReleaseSink.GlobalTool);
        Assert.Equal(tool.BuiltIdentity, tool.IdentityRecordedAtWrite);
        Assert.Equal(tool.BuiltIdentity, claim.IdentityOf(ReleaseSink.GlobalTool));
    }

    // A fresh run finding a sink that already holds the version with no claim at all refuses
    // before claiming anything.
    [Fact]
    public async Task Publish_FailsWithVersionAlreadyExists_WhenAnUnclaimedSinkHoldsTheVersion()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        SinkFor(sinks, ReleaseSink.GlobalTool).HeldIdentity = "sha256:someone-else";

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains(nameof(ReleaseSink.GlobalTool), ex.Message);
        Assert.Empty(claimStore.Refs);
        Assert.Equal(0, claimStore.DraftsCreated);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // A failure to record a sink's identity fails naming the sink, and the sink is not written.
    [Fact]
    public async Task Publish_FailsWithSinkPublishFailed_WhenTheIdentityCannotBeRecorded()
    {
        var (pipeline, claimStore, _, _, _, _, sinks) = Build();
        claimStore.FailRecordIdentity = new InvalidOperationException("release edit rejected");

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkPublishFailed, ex.Code);
        Assert.Equal(ReleaseSink.ImageVersionedTag, ex.Sink);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
        Assert.Equal(ClaimState.Draft, Assert.Single(claimStore.Claims).Value.State);
    }

    // A resume whose earlier attempt already created the git tag at this commit continues.
    [Fact]
    public async Task Publish_ResumesWhenTheGitTagIsAlreadyAtTheCommit()
    {
        var (pipeline, claimStore, _, gitTagChecker, _, _, _) = Build();
        gitTagChecker.Seed(Version, CommitSha);
        claimStore.SeedRef(Version, CommitSha);
        claimStore.Seed(Claim(CommitSha, ClaimState.Draft));

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Equal(ClaimState.Published, claim.State);
    }

    // Completion re-run: the release is published for this commit and complete, so only latest
    // moves; nothing else is written.
    [Fact]
    public async Task Publish_CompletionRerun_MovesOnlyLatest()
    {
        var (pipeline, claimStore, _, gitTagChecker, highestPublished, _, sinks) = Build();
        var seeded = SeedCompleteRelease(claimStore, gitTagChecker, highestPublished, sinks);

        var claim = await pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString()));

        Assert.Same(seeded, claim);
        Assert.True(SinkFor(sinks, ReleaseSink.ImageLatestTag).WasCalled);
        Assert.All(sinks.Where(s => s.Sink != ReleaseSink.ImageLatestTag), s => Assert.False(s.WasCalled));
        Assert.Equal(0, claimStore.DraftsCreated);
        Assert.Empty(claimStore.IdentitiesRecorded);
    }

    // Completion re-run: latest is never moved back to a release that is no longer the highest.
    [Fact]
    public async Task Publish_CompletionRerun_FailsWithVersionAlreadyExists_WhenNotTheHighestRelease()
    {
        var (pipeline, claimStore, _, gitTagChecker, highestPublished, _, sinks) = Build();
        SeedCompleteRelease(claimStore, gitTagChecker, highestPublished, sinks);
        highestPublished.Highest = new ReleaseVersion(2, 1, 0, null);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains("2.1.0", ex.Message);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // Completion re-run: a sink holding another artifact than the claim records fails naming it.
    [Fact]
    public async Task Publish_CompletionRerun_FailsWithSinkArtifactMismatch_WhenASinkDiffers()
    {
        var (pipeline, claimStore, _, gitTagChecker, highestPublished, _, sinks) = Build();
        SeedCompleteRelease(claimStore, gitTagChecker, highestPublished, sinks);
        SinkFor(sinks, ReleaseSink.GlobalTool).HeldIdentity = "sha256:replaced";

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkArtifactMismatch, ex.Code);
        Assert.Equal(ReleaseSink.GlobalTool, ex.Sink);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // Completion re-run: a published release missing from a versioned sink fails naming it.
    [Fact]
    public async Task Publish_CompletionRerun_FailsWithSinkArtifactMismatch_WhenASinkDoesNotHoldTheVersion()
    {
        var (pipeline, claimStore, _, gitTagChecker, highestPublished, _, sinks) = Build();
        SeedCompleteRelease(claimStore, gitTagChecker, highestPublished, sinks);
        SinkFor(sinks, ReleaseSink.PowerShellModule).HeldIdentity = null;

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkArtifactMismatch, ex.Code);
        Assert.Equal(ReleaseSink.PowerShellModule, ex.Sink);
        Assert.Contains("does not hold", ex.Message);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // Completion re-run: the git tag must point at the release's commit.
    [Fact]
    public async Task Publish_CompletionRerun_FailsWithTagPointsElsewhere_WhenTheTagIsAtAnotherCommit()
    {
        var (pipeline, claimStore, _, gitTagChecker, highestPublished, _, sinks) = Build();
        SeedCompleteRelease(claimStore, gitTagChecker, highestPublished, sinks);
        gitTagChecker.Seed(Version, OtherCommitSha);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.TagPointsElsewhere, ex.Code);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // Completion re-run: a published release with no git tag is not completed.
    [Fact]
    public async Task Publish_CompletionRerun_FailsWithVersionAlreadyExists_WhenTheTagIsAbsent()
    {
        var (pipeline, claimStore, _, _, highestPublished, _, sinks) = Build();
        SeedCompleteRelease(claimStore, new FakeGitTagChecker(), highestPublished, sinks);

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.VersionAlreadyExists, ex.Code);
        Assert.Contains("git tag", ex.Message);
        Assert.All(sinks, s => Assert.False(s.WasCalled));
    }

    // Completion re-run: a failure to move latest fails naming ImageLatestTag.
    [Fact]
    public async Task Publish_CompletionRerun_FailsWithSinkPublishFailed_WhenLatestCannotMove()
    {
        var (pipeline, claimStore, _, gitTagChecker, highestPublished, _, sinks) = Build();
        SeedCompleteRelease(claimStore, gitTagChecker, highestPublished, sinks);
        SinkFor(sinks, ReleaseSink.ImageLatestTag).ShouldFail = true;

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            pipeline.PublishAsync(Version, CommitSha, TestNotes.Valid, TestManifest.Empty(Version.ToPackageString())));

        Assert.Equal(ReleaseErrorCode.SinkPublishFailed, ex.Code);
        Assert.Equal(ReleaseSink.ImageLatestTag, ex.Sink);
    }

    private sealed class OrderRecordingSink : IVersionedSinkPublisher
    {
        private readonly System.Collections.Generic.List<string> _callOrder;

        public OrderRecordingSink(ReleaseSink sink, System.Collections.Generic.List<string> callOrder)
        {
            Sink = sink;
            _callOrder = callOrder;
        }

        public ReleaseSink Sink { get; }

        public string BuiltIdentity => $"sha256:{Sink}";

        public Task<string?> FindPublishedIdentityAsync(ReleaseVersion version) => Task.FromResult<string?>(null);

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
