using Xunit;

using Utilities;

namespace Common.Tests.Utilities;

public class GitTests
{
    [Fact]
    public void CreateTag_Throws_ForNullTag()
    {
        string? tag = null;

        var ex = Assert.Throws<ArgumentException>(() => Git.CreateTag(tag!));

        Assert.Equal("tag", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad tag")]
    [InlineData("tag/with/slash")]
    public void CreateTag_Throws_ForInvalidTag(string tag)
    {
        var ex = Assert.Throws<ArgumentException>(() => Git.CreateTag(tag));

        Assert.Equal("tag", ex.ParamName);
    }

    [Fact]
    public void Urls_ReturnsNullBranchAndCommit_WhenUnknown()
    {
        var (repoUrl, branchUrl, commitUrl) = Git.Urls("unknown", "unknown");

        Assert.Null(branchUrl);
        Assert.Null(commitUrl);
        _ = repoUrl; // no assertion; may be null or a resolved repo URL
    }
}
