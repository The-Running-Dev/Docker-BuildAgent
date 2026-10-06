using Xunit;

using CommonUtil = Utilities.Common;

namespace Common.Tests.Utilities;

public class CommonTests : IDisposable
{
    private readonly string _rootDir;

    public CommonTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-common-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void GetVersion_ReturnsDefault_WhenGitDirectoryMissing()
    {
        var version = CommonUtil.GetVersion(_rootDir);

        Assert.Equal("0.0.0", version.Version);
        Assert.Equal("0.0.0", version.FullVersion);
        Assert.Equal(string.Empty, version.Date);
        Assert.Equal(string.Empty, version.Hash);
    }

    [Fact]
    public void GetVersion_WithNoGit_DoesNotRunGitVersion()
    {
        var version = CommonUtil.GetVersion(_rootDir, _ => throw new InvalidOperationException("must not run"));

        Assert.Equal("0.0.0", version.Version);
    }

    [Fact]
    public void GetVersion_WithAGitFile_RunsGitVersion()
    {
        // A worktree or submodule: `.git` is a file that points at the real repository.
        File.WriteAllText(Path.Combine(_rootDir, ".git"), "gitdir: ../main/.git/worktrees/feature");

        var version = CommonUtil.GetVersion(_rootDir, _ => GitVersionJson("2.3.4"));

        Assert.Equal("2.3.4", version.Version);
    }

    [Fact]
    public void GetVersion_WithAGitDirectory_RunsGitVersion()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, ".git"));

        var version = CommonUtil.GetVersion(_rootDir, _ => GitVersionJson("2.3.4"));

        Assert.Equal("2.3.4", version.Version);
    }

    [Theory]
    [InlineData("no commits found on the current branch")]
    [InlineData("Process 'dotnet-gitversion' exited with code 1")]
    public void GetVersion_WhenGitVersionFails_FailsInsteadOfSubstitutingAVersion(string error)
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, ".git"));

        var exception = Record.Exception(() => CommonUtil.GetVersion(_rootDir, _ => throw new InvalidOperationException(error)));

        Assert.NotNull(exception);
    }

    [Fact]
    public void GetVersion_WhenGitVersionReturnsNoVersion_Fails()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, ".git"));

        var exception = Record.Exception(() => CommonUtil.GetVersion(_rootDir, _ => "{}"));

        Assert.NotNull(exception);
    }

    private static string GitVersionJson(string version) =>
        $"{{\"SemVer\":\"{version}\",\"FullSemVer\":\"{version}\",\"CommitDate\":\"2026-10-06\",\"ShortSha\":\"abc1234\"}}";
}
