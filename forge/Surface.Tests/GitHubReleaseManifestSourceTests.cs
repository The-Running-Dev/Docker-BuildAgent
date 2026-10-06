using Xunit;

namespace Surface.Tests;

/// <summary>
/// The baseline is the highest published stable release below the candidate (I8), by version
/// precedence and never by publish date.
/// </summary>
public class GitHubReleaseManifestSourceTests
{
    private static string? Select(string candidate, params string?[] tags)
    {
        Assert.True(SemanticVersion.TryParse(candidate, out var version));

        return GitHubReleaseManifestSource.SelectBaselineTag(tags, version);
    }

    // Listed in publish order: v2.0.5 was back-published after v2.1.0, and is not the baseline for 2.2.0.
    [Fact]
    public void SelectBaselineTag_PicksTheHighestVersion_NotTheLatestPublished()
    {
        Assert.Equal("v2.1.0", Select("2.2.0", "v2.0.0", "v2.1.0", "v2.0.5"));
    }

    [Fact]
    public void SelectBaselineTag_IgnoresReleasesAtOrAboveTheCandidate()
    {
        Assert.Equal("v2.1.0", Select("2.2.0", "v2.1.0", "v2.2.0", "v3.0.0"));
    }

    [Fact]
    public void SelectBaselineTag_ComparesNumbersNumerically()
    {
        Assert.Equal("v2.10.0", Select("2.11.0", "v2.9.0", "v2.10.0"));
    }

    // A release counts its breaking changes and deprecations from the previous stable release, so its
    // own release candidates are never its baseline.
    [Fact]
    public void SelectBaselineTag_SkipsPreReleases()
    {
        Assert.Equal("v1.9.0", Select("2.0.0", "v2.0.0-beta.10", "v2.0.0-rc.1", "v1.9.0"));
        Assert.Equal("v1.9.0", Select("2.0.0-rc2", "v2.0.0-rc1", "v1.9.0"));
        Assert.Null(Select("2.0.0", "v2.0.0-rc1", "v1.0.0-rc1"));
    }

    [Fact]
    public void SelectBaselineTag_ATaggedPreReleaseCandidateSitsBelowItsRelease()
    {
        Assert.Equal("v1.9.0", Select("2.0.0-rc1", "v1.9.0", "v2.0.0"));
    }

    [Fact]
    public void SelectBaselineTag_SkipsTagsThatAreNotVersions()
    {
        Assert.Equal("v2.0.0", Select("2.1.0", "latest", null, "", "v2.0.0", "nightly-2.5"));
    }

    [Fact]
    public void SelectBaselineTag_WithNothingBelowTheCandidate_ReturnsNull()
    {
        Assert.Null(Select("2.0.0", "v2.0.0", "v2.1.0"));
    }
}
