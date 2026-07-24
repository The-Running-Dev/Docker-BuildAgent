using Xunit;

using Entities;

namespace Common.Tests.Entities;

public class VersionInfoTests
{
    [Fact]
    public void ToString_ReturnsVersion()
    {
        var info = new VersionInfo { Version = "1.2.3" };

        Assert.Equal("1.2.3", info.ToString());
    }
}
