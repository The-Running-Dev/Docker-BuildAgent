using Xunit;

using Entities;

namespace Common.Tests.Entities;

public class MapFileValueTests
{
    [Fact]
    public void ToString_ReturnsKeyValue()
    {
        var value = new MapFileValue("Key", "template", "Value");

        Assert.Equal("Key=Value", value.ToString());
    }
}
