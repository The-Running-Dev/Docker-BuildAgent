using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Moq;

using Octokit;

using Xunit;

namespace Release.Tests;

/// <summary>
/// The highest published release is the highest published stable release (I6, and the release
/// <c>latest</c> names), by version precedence rather than publish date.
/// </summary>
public sealed class GitHubHighestPublishedVersionSourceTests
{
    private static Task<ReleaseVersion?> Highest(params (string Tag, bool Draft)[] releases)
    {
        var list = new List<Octokit.Release>();
        foreach (var (tag, draft) in releases)
        {
            list.Add(new Octokit.Release(
                "url", "html", "assets", "upload", list.Count + 1, "node", tag, "main", tag, "body",
                draft, tag.Contains('-'), DateTimeOffset.UtcNow, draft ? null : DateTimeOffset.UtcNow,
                null, "tar", "zip", Array.Empty<ReleaseAsset>()));
        }

        var releasesClient = new Mock<IReleasesClient>();
        releasesClient.Setup(r => r.GetAll("owner", "repo")).ReturnsAsync(list);
        var repository = new Mock<IRepositoriesClient>();
        repository.SetupGet(r => r.Release).Returns(releasesClient.Object);
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.Repository).Returns(repository.Object);

        return new GitHubHighestPublishedVersionSource("owner", "repo", "token", client.Object).GetHighestPublishedAsync();
    }

    [Fact]
    public async Task PicksTheHighestVersion_NotTheLatestPublished()
    {
        Assert.Equal(new ReleaseVersion(2, 10, 0, null), await Highest(("v2.9.0", false), ("v2.10.0", false), ("v2.0.5", false)));
    }

    // A 3.0.0-rc1 is not the highest release: it does not stop a 2.x patch, and never becomes latest.
    [Fact]
    public async Task SkipsPreReleases()
    {
        Assert.Equal(new ReleaseVersion(2, 1, 0, null), await Highest(("v2.1.0", false), ("v3.0.0-rc1", false), ("v2.2.0-beta1", false)));
        Assert.Null(await Highest(("v1.1.0-2", false)));
    }

    [Fact]
    public async Task SkipsDraftsAndTagsThatAreNotVersions()
    {
        Assert.Equal(new ReleaseVersion(2, 0, 0, null), await Highest(("v2.0.0", false), ("v3.0.0", true), ("nightly", false)));
    }
}
