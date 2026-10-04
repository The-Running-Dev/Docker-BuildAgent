using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Moq;

using Octokit;

using Xunit;

namespace Release.Tests;

/// <summary>
/// The claim ref half of <see cref="GitHubReleaseClaimStore"/> (F5, I13): the refs API is the atomic
/// arbiter, so these tests pin how the store reads its answers.
/// </summary>
public sealed class GitHubReleaseClaimStoreTests
{
    private const string Owner = "owner";
    private const string Repo = "repo";
    private const string CommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherCommitSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly ReleaseVersion Version = new(2, 0, 0, null);

    private static (GitHubReleaseClaimStore Store, Mock<IReferencesClient> References) Build()
    {
        var references = new Mock<IReferencesClient>(MockBehavior.Strict);
        var git = new Mock<IGitDatabaseClient>();
        git.SetupGet(g => g.Reference).Returns(references.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Git).Returns(git.Object);
        return (new GitHubReleaseClaimStore(Owner, Repo, "token", client.Object), references);
    }

    private static Reference Ref(string name, string sha) =>
        new(name, "node", "url", new TagObject("node", "url", null, name, sha, null, null, TaggedType.Commit));

    private static ApiValidationException RefAlreadyExists()
    {
        var response = new Mock<IResponse>();
        response.SetupGet(r => r.StatusCode).Returns((HttpStatusCode)422);
        response.SetupGet(r => r.Body).Returns("{\"message\":\"Reference already exists\"}");
        response.SetupGet(r => r.ContentType).Returns("application/json");
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        return new ApiValidationException(response.Object);
    }

    [Fact]
    public void ClaimRefName_IsUnderTheReleaseClaimsNamespace()
    {
        Assert.Equal("refs/release-claims/v2.0.0", GitHubReleaseClaimStore.ClaimRefName(Version));
    }

    [Fact]
    public async Task CreateClaimRef_CreatesTheRefAtTheCommit()
    {
        var (store, references) = Build();
        references
            .Setup(r => r.Create(Owner, Repo, It.Is<NewReference>(n => n.Ref == "refs/release-claims/v2.0.0" && n.Sha == CommitSha)))
            .ReturnsAsync(Ref("refs/release-claims/v2.0.0", CommitSha));

        var claimRef = await store.CreateClaimRefAsync(Version, CommitSha);

        Assert.Equal(new ClaimRef(CommitSha, Created: true), claimRef);
    }

    [Theory]
    [InlineData(CommitSha)]
    [InlineData(OtherCommitSha)]
    public async Task CreateClaimRef_ReturnsTheExistingRef_WhenTheRefsApiRefusesAnExistingRef(string existingSha)
    {
        var (store, references) = Build();
        references.Setup(r => r.Create(Owner, Repo, It.IsAny<NewReference>())).ThrowsAsync(RefAlreadyExists());
        references
            .Setup(r => r.GetAllForSubNamespace(Owner, Repo, "release-claims"))
            .ReturnsAsync(new[] { Ref("refs/release-claims/v2.0.0", existingSha) });

        var claimRef = await store.CreateClaimRefAsync(Version, CommitSha);

        Assert.Equal(new ClaimRef(existingSha, Created: false), claimRef);
    }

    [Fact]
    public async Task CreateClaimRef_FailsWithClaimCreationFailed_WhenRefusedAndNoRefExists()
    {
        var (store, references) = Build();
        references.Setup(r => r.Create(Owner, Repo, It.IsAny<NewReference>())).ThrowsAsync(RefAlreadyExists());
        references
            .Setup(r => r.GetAllForSubNamespace(Owner, Repo, "release-claims"))
            .ReturnsAsync(Array.Empty<Reference>());

        var ex = await Assert.ThrowsAsync<ReleaseException>(() => store.CreateClaimRefAsync(Version, CommitSha));

        Assert.Equal(ReleaseErrorCode.ClaimCreationFailed, ex.Code);
    }

    [Fact]
    public async Task CreateClaimRef_FailsWithClaimCreationFailed_WhenTheRefsApiFails()
    {
        var (store, references) = Build();
        references
            .Setup(r => r.Create(Owner, Repo, It.IsAny<NewReference>()))
            .ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() => store.CreateClaimRefAsync(Version, CommitSha));

        Assert.Equal(ReleaseErrorCode.ClaimCreationFailed, ex.Code);
    }

    [Fact]
    public async Task FindClaimRef_ReturnsNull_WhenTheNamespaceDoesNotExist()
    {
        var (store, references) = Build();
        references
            .Setup(r => r.GetAllForSubNamespace(Owner, Repo, "release-claims"))
            .ThrowsAsync(new NotFoundException("Not Found", HttpStatusCode.NotFound));

        Assert.Null(await store.FindClaimRefAsync(Version));
    }

    [Fact]
    public async Task FindClaimRef_MatchesTheRefExactly_NotByPrefix()
    {
        var (store, references) = Build();
        references
            .Setup(r => r.GetAllForSubNamespace(Owner, Repo, "release-claims"))
            .ReturnsAsync(new[] { Ref("refs/release-claims/v2.0.0-rc.1", OtherCommitSha) });

        Assert.Null(await store.FindClaimRefAsync(Version));
    }

    [Fact]
    public async Task FindClaimRef_ReturnsTheCommitOfTheMatchingRef()
    {
        var (store, references) = Build();
        references
            .Setup(r => r.GetAllForSubNamespace(Owner, Repo, "release-claims"))
            .ReturnsAsync(new[]
            {
                Ref("refs/release-claims/v2.0.0-rc.1", OtherCommitSha),
                Ref("refs/release-claims/v2.0.0", CommitSha),
            });

        Assert.Equal(CommitSha, await store.FindClaimRefAsync(Version));
    }

    [Fact]
    public async Task FindClaimRef_FailsWithClaimCreationFailed_WhenTheRefsCannotBeRead()
    {
        var (store, references) = Build();
        references
            .Setup(r => r.GetAllForSubNamespace(Owner, Repo, "release-claims"))
            .ThrowsAsync(new InvalidOperationException("network"));

        var ex = await Assert.ThrowsAsync<ReleaseException>(() => store.FindClaimRefAsync(Version));

        Assert.Equal(ReleaseErrorCode.ClaimCreationFailed, ex.Code);
    }

    private static (GitHubReleaseClaimStore Store, Mock<IReleasesClient> Releases) BuildWithReleases()
    {
        var releases = new Mock<IReleasesClient>(MockBehavior.Strict);
        var repository = new Mock<IRepositoriesClient>();
        repository.SetupGet(r => r.Release).Returns(releases.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Repository).Returns(repository.Object);
        return (new GitHubReleaseClaimStore(Owner, Repo, "token", client.Object), releases);
    }

    private static Octokit.Release GitHubRelease(long id, string commitSha, bool draft, string body) =>
        new("url", "html", "assets", "upload", id, "node", Version.ToTagString(), commitSha, Version.ToTagString(), body,
            draft, false, DateTimeOffset.UnixEpoch, null, null, "tarball", "zipball", Array.Empty<ReleaseAsset>());

    private static ReleaseClaim DraftClaim() =>
        new(Version, CommitSha, ClaimState.Draft, "Notes", TestSupport.TestManifest.Empty(Version.ToPackageString()));

    // F4: the identity is written into the draft's body, and a later read of the release returns it
    // with the identities already recorded. The draft for this commit is edited, not a stale one.
    [Fact]
    public async Task RecordIdentity_WritesTheIdentityIntoTheDraft_AndFindClaimReadsItBack()
    {
        var (store, releases) = BuildWithReleases();
        releases
            .Setup(r => r.GetAll(Owner, Repo))
            .ReturnsAsync(new[]
            {
                GitHubRelease(6, OtherCommitSha, draft: true, "stale"),
                GitHubRelease(7, CommitSha, draft: true, "Notes"),
            });
        string? editedBody = null;
        releases
            .Setup(r => r.Edit(Owner, Repo, 7, It.IsAny<ReleaseUpdate>()))
            .Callback<string, string, long, ReleaseUpdate>((_, _, _, update) => editedBody = update.Body)
            .ReturnsAsync(GitHubRelease(7, CommitSha, draft: true, "Notes"));

        var claim = DraftClaim().WithIdentity(ReleaseSink.ImageVersionedTag, "sha256:image");
        var updated = await store.RecordIdentityAsync(claim, ReleaseSink.GlobalTool, "sha256:tool");

        Assert.Equal("sha256:tool", updated.IdentityOf(ReleaseSink.GlobalTool));
        Assert.NotNull(editedBody);
        Assert.Contains("release-claim:artifact-identities", editedBody);

        releases
            .Setup(r => r.GetAll(Owner, Repo))
            .ReturnsAsync(new[] { GitHubRelease(7, CommitSha, draft: true, editedBody!) });

        var read = await store.FindClaimAsync(Version);

        Assert.NotNull(read);
        Assert.Equal("Notes", read!.Notes);
        Assert.Equal(Version.ToPackageString(), read.CandidateManifest.ProductVersion);
        Assert.Equal(
            new Dictionary<ReleaseSink, string>
            {
                [ReleaseSink.ImageVersionedTag] = "sha256:image",
                [ReleaseSink.GlobalTool] = "sha256:tool",
            },
            read.ArtifactIdentities);
    }

    // An identities marker that cannot be read yields no identities, so a held sink fails closed.
    [Theory]
    [InlineData("Notes")]
    [InlineData("Notes\n\n<!-- release-claim:artifact-identities\n{not json\nrelease-claim:artifact-identities -->\n")]
    [InlineData("Notes\n\n<!-- release-claim:artifact-identities\n{\"NoSuchSink\":\"sha256:x\",\"GlobalTool\":\"\"}\nrelease-claim:artifact-identities -->\n")]
    [InlineData("Notes\n\n<!-- release-claim:artifact-identities\n[\"GlobalTool\"]\nrelease-claim:artifact-identities -->\n")]
    public async Task FindClaim_ReadsNoIdentities_WhenTheMarkerIsAbsentOrUnreadable(string body)
    {
        var (store, releases) = BuildWithReleases();
        releases
            .Setup(r => r.GetAll(Owner, Repo))
            .ReturnsAsync(new[] { GitHubRelease(7, CommitSha, draft: true, body) });

        var read = await store.FindClaimAsync(Version);

        Assert.NotNull(read);
        Assert.Empty(read!.ArtifactIdentities);
    }

    [Fact]
    public async Task RecordIdentity_FailsWithClaimCreationFailed_WhenNoDraftExistsForTheCommit()
    {
        var (store, releases) = BuildWithReleases();
        releases
            .Setup(r => r.GetAll(Owner, Repo))
            .ReturnsAsync(new[] { GitHubRelease(6, OtherCommitSha, draft: true, "stale") });

        var ex = await Assert.ThrowsAsync<ReleaseException>(() =>
            store.RecordIdentityAsync(DraftClaim(), ReleaseSink.GlobalTool, "sha256:tool"));

        Assert.Equal(ReleaseErrorCode.ClaimCreationFailed, ex.Code);
    }
}
