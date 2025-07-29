using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Entities;
using Services;

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
}