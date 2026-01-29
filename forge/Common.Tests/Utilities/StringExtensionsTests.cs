using Xunit;

using Extensions;

namespace Common.Tests.Utilities;

public class StringExtensionsTests
{
    [Theory]
    [InlineData(null, "(no error body)")]
    [InlineData("", "(no error body)")]
    [InlineData("  ", "(no error body)")]
    public void SanitizeForLog_ReturnsPlaceholder_WhenNullOrWhitespace(string? input, string expected)
    {
        var result = input.SanitizeForLog();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeForLog_FlattensNewlines_AndTruncates()
    {
        var input = "line1\nline2\r\nline3";
        var result = input.SanitizeForLog(maxLength: 10);

        Assert.DoesNotContain("\n", result);
        Assert.DoesNotContain("\r", result);
        Assert.StartsWith("line1 line", result);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void SanitizeForLog_UsesCustomPlaceholder_WhenProvided()
    {
        string? input = null;
        var result = input.SanitizeForLog(emptyPlaceholder: "(empty)");

        Assert.Equal("(empty)", result);
    }

    [Fact]
    public void SanitizeForLog_DoesNotTruncate_WhenMaxLengthIsZero()
    {
        var input = "line1\nline2";
        var result = input.SanitizeForLog(maxLength: 0);

        Assert.Equal("line1 line2", result);
    }

    [Theory]
    [InlineData("https://ghcr.io/owner/repo", "ghcr.io")]
    [InlineData("https://github.com/owner/repo", "github.com")]
    [InlineData("https://registry.example.com/org/repo", "registry.example.com")]
    [InlineData("ghcr.io/owner/repo", "ghcr.io")]
    [InlineData("registry.example.com", "registry.example.com")]
    public void GetRegistryServer_ExtractsHost(string input, string expected)
    {
        var result = input.GetRegistryServer();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("npm install", true, "npm", "install")]
    [InlineData("pnpm run build", true, "pnpm", "run build")]
    [InlineData("YARN add react", true, "YARN", "add react")]
    [InlineData("pwsh -File script.ps1", false, null, null)]
    public void TryParsePackageManagerCommand_ParsesCommands(string input, bool expectedSuccess, string? expectedPm, string? expectedCommand)
    {
        var success = input.TryParsePackageManagerCommand(out var pm, out var command);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedPm, pm);
        Assert.Equal(expectedCommand, command);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParsePackageManagerCommand_ReturnsFalse_ForEmpty(string? input)
    {
        var success = input.TryParsePackageManagerCommand(out var pm, out var command);

        Assert.False(success);
        Assert.Null(pm);
        Assert.Null(command);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", "owner/repo")]
    [InlineData("https://github.com/owner/repo.git", "owner/repo")]
    [InlineData("https://ghcr.io/owner/repo", "owner/repo")]
    [InlineData("https://github.com/owner/repo/issues/1", "owner/repo")]
    [InlineData("owner/repo", "owner/repo")]
    [InlineData("owner", "owner")]
    public void GetGitHubRepoSlug_Normalizes(string input, string expected)
    {
        var result = input.GetGitHubRepoSlug();

        Assert.Equal(expected, result);
    }
}
