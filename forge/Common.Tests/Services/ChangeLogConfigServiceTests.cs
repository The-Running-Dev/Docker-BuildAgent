using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

using Entities;
using Services;

namespace Common.Tests.Services;

/// <summary>
/// Unit tests for the ChangeLogConfigService class.
/// </summary>
public class ChangeLogConfigServiceTests
{
    private readonly Mock<IGitService> _mockGitService;
    private readonly Mock<ILogger<ChangeLogConfigService>> _mockLogger;
    private readonly ChangeLogConfigService _service;

    public ChangeLogConfigServiceTests()
    {
        _mockGitService = new Mock<IGitService>();
        _mockLogger = new Mock<ILogger<ChangeLogConfigService>>();
        _service = new ChangeLogConfigService(_mockGitService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullGitService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ChangeLogConfigService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ChangeLogConfigService(_mockGitService.Object, null!));
    }

    [Fact]
    public void Create_WithNullInput_ReturnsLastTagConfig()
    {
        // Arrange
        const string expectedTag = "v1.0.0";
        _mockGitService.Setup(x => x.GetLastTag()).Returns(expectedTag);
        
        // Act
        var config = _service.Create(null);
        
        // Assert
        Assert.Equal(ChangeLogSource.LastTag, config.Source);
        Assert.Equal(expectedTag, config.Tag);
        _mockGitService.Verify(x => x.GetLastTag(), Times.Once);
    }

    [Fact]
    public void Create_WithEmptyString_ReturnsLastTagConfig()
    {
        // Arrange
        const string expectedTag = "v2.0.0";
        _mockGitService.Setup(x => x.GetLastTag()).Returns(expectedTag);
        
        // Act
        var config = _service.Create("");
        
        // Assert
        Assert.Equal(ChangeLogSource.LastTag, config.Source);
        Assert.Equal(expectedTag, config.Tag);
        _mockGitService.Verify(x => x.GetLastTag(), Times.Once);
    }

    [Fact]
    public void Create_WithWhitespaceString_ReturnsLastTagConfig()
    {
        // Arrange
        const string expectedTag = "v3.0.0";
        _mockGitService.Setup(x => x.GetLastTag()).Returns(expectedTag);
        
        // Act
        var config = _service.Create("   ");
        
        // Assert
        Assert.Equal(ChangeLogSource.LastTag, config.Source);
        Assert.Equal(expectedTag, config.Tag);
        _mockGitService.Verify(x => x.GetLastTag(), Times.Once);
    }

    [Fact]
    public void Create_WithNullReturnFromGitService_ReturnsLastTagConfigWithNullTag()
    {
        // Arrange
        _mockGitService.Setup(x => x.GetLastTag()).Returns((string?)null);
        
        // Act
        var config = _service.Create(null);
        
        // Assert
        Assert.Equal(ChangeLogSource.LastTag, config.Source);
        Assert.Null(config.Tag);
        _mockGitService.Verify(x => x.GetLastTag(), Times.Once);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("All")]
    [InlineData("aLL")]
    public void Create_WithAllKeyword_ReturnsAllConfigWithNullTag(string input)
    {
        // Act
        var config = _service.Create(input);
        
        // Assert
        Assert.Equal(ChangeLogSource.All, config.Source);
        Assert.Null(config.Tag);
        _mockGitService.Verify(x => x.GetLastTag(), Times.Never);
    }

    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("release-2023")]
    [InlineData("custom-tag")]
    public void Create_WithSpecificTag_ReturnsSpecificTagConfig(string input)
    {
        // Act
        var config = _service.Create(input);
        
        // Assert
        Assert.Equal(ChangeLogSource.SpecificTag, config.Source);
        Assert.Equal(input, config.Tag);
        _mockGitService.Verify(x => x.GetLastTag(), Times.Never);
    }

    [Fact]
    public void Create_WithSpecificTagWithWhitespace_TrimsAndReturnsSpecificTagConfig()
    {
        // Arrange
        const string inputWithWhitespace = "  v1.0.0  ";
        const string expectedTag = "v1.0.0";
        
        // Act
        var config = _service.Create(inputWithWhitespace);
        
        // Assert
        Assert.Equal(ChangeLogSource.SpecificTag, config.Source);
        Assert.Equal(expectedTag, config.Tag);
        _mockGitService.Verify(x => x.GetLastTag(), Times.Never);
    }
}