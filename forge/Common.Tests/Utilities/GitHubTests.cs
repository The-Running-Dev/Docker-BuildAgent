using Xunit;

using Utilities;

namespace Common.Tests.Utilities;

public class GitHubTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo", "https://api.github.com/repos/owner/repo/releases")]
    [InlineData("https://github.com/owner/repo.git", "https://api.github.com/repos/owner/repo/releases")]
    [InlineData("https://ghcr.io/owner/repo", "https://api.github.com/repos/owner/repo/releases")]
    [InlineData("owner/repo", "https://api.github.com/repos/owner/repo/releases")]
    public void BuildReleaseApiUrl_NormalizesRepositoryUrl(string input, string expected)
    {
        var result = GitHub.BuildReleaseApiUrl(input);

        Assert.Equal(expected, result);
    }
}
