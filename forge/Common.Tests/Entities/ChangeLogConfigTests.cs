using Xunit;

using Entities;

namespace Common.Tests.Entities;

public class ChangeLogConfigTests
{
    [Fact]
    public void Constructor_WithDefaultParameters_CreatesInstanceWithUnknownSource()
    {
        // Act
        var config = new ChangeLogConfig();
        
        // Assert
        Assert.Equal(ChangeLogSource.LastTag, config.Source); // Default value
        Assert.Null(config.Tag);
    }

    [Fact]
    public void Constructor_WithSourceAndTag_SetsPropertiesCorrectly()
    {
        // Arrange
        const string expectedTag = "v1.0.0";
        const ChangeLogSource expectedSource = ChangeLogSource.SpecificTag;
        
        // Act
        var config = new ChangeLogConfig(expectedSource, expectedTag);
        
        // Assert
        Assert.Equal(expectedSource, config.Source);
        Assert.Equal(expectedTag, config.Tag);
    }

    [Fact]
    public void Constructor_WithSourceOnly_SetsSourceAndNullTag()
    {
        // Arrange
        const ChangeLogSource expectedSource = ChangeLogSource.All;
        
        // Act
        var config = new ChangeLogConfig(expectedSource);
        
        // Assert
        Assert.Equal(expectedSource, config.Source);
        Assert.Null(config.Tag);
    }

    [Fact]
    public void FromString_ReturnsLastTag_ByDefault()
    {
        var config = ChangeLogConfig.FromString(null);

        Assert.Equal(ChangeLogSource.LastTag, config.Source);
        Assert.Null(config.Tag);
    }

    [Fact]
    public void FromString_ReturnsAll_ForAllKeyword()
    {
        var config = ChangeLogConfig.FromString("all");

        Assert.Equal(ChangeLogSource.All, config.Source);
        Assert.Null(config.Tag);
    }

    [Fact]
    public void FromString_ReturnsSpecificTag_ForOtherValues()
    {
        var config = ChangeLogConfig.FromString("v1.2.3");

        Assert.Equal(ChangeLogSource.SpecificTag, config.Source);
        Assert.Equal("v1.2.3", config.Tag);
    }
}