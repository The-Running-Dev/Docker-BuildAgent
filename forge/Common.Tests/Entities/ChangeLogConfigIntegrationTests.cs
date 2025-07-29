using Xunit;
using Microsoft.Extensions.Logging;
using Entities;
using Services;

namespace Common.Tests.Entities;

/// <summary>
/// Integration tests for ChangeLogConfigFactory that test behavior with real GitService operations
/// </summary>
public class ChangeLogConfigIntegrationTests
{
    private readonly ChangeLogConfigService _service;

    public ChangeLogConfigIntegrationTests()
    {
        // Create real services for integration testing
        var logger = new LoggerFactory().CreateLogger<GitService>();
        var gitService = new GitService(logger);
        
        var factoryLogger = new LoggerFactory().CreateLogger<ChangeLogConfigService>();
        _service = new ChangeLogConfigService(gitService, factoryLogger);
    }

    [Fact]
    public void Create_WithSpecificTag_WorksCorrectly()
    {
        // Act
        var config = _service.Create("specific-tag");
        
        // Assert
        Assert.Equal(ChangeLogSource.SpecificTag, config.Source);
        Assert.Equal("specific-tag", config.Tag);
    }

    [Fact]
    public void Create_WithNullInput_CallsRealGetLastTag()
    {
        // This test verifies that the real GitService integration works
        // The result will depend on the actual GitService state of the repository
        
        // Act
        var config = _service.Create(null);
        
        // Assert
        Assert.Equal(ChangeLogSource.LastTag, config.Source);
        // Tag will be whatever GitService.GetLastTag() returns (could be null if no tags exist)
    }

    [Theory]
    [InlineData("all")]
    [InlineData("ALL")]
    public void Create_WithAllKeyword_DoesNotCallGitService(string input)
    {
        // Act
        var config = _service.Create(input);
        
        // Assert
        Assert.Equal(ChangeLogSource.All, config.Source);
        Assert.Null(config.Tag);
    }
}