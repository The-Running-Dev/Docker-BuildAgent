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
}
