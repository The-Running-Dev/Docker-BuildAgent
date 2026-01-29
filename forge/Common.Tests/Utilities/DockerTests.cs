using Xunit;

using Utilities;

namespace Common.Tests.Utilities;

public class DockerTests
{
    [Theory]
    [InlineData("https://ghcr.io/owner/repo", "ghcr.io")]
    [InlineData("https://github.com/owner/repo", "github.com")]
    [InlineData("ghcr.io/owner/repo", "ghcr.io")]
    [InlineData("registry.example.com", "registry.example.com")]
    public void GetRegistryServerForLogin_ExtractsHost(string input, string expected)
    {
        var result = Docker.GetRegistryServerForLogin(input);

        Assert.Equal(expected, result);
    }
}
