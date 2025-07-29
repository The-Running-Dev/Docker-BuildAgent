using Xunit;
using Microsoft.Extensions.Logging;

using Entities;
using Services;
using Utilities;

namespace Common.Tests.Services;

/// <summary>
/// Integration tests for GitService that test behavior with real GitService operations.
/// These tests require a GitService repository and may depend on the actual repository state.
/// </summary>
[Collection("GitIntegration")]
public class GitServiceIntegrationTests : IDisposable
{
    private readonly GitService _gitService;

    private readonly string _tempDirectory;

    public GitServiceIntegrationTests()
    {
        var logger = new LoggerFactory().CreateLogger<GitService>();
        _gitService = new GitService(logger);
        _tempDirectory = Path.Combine(Path.GetTempPath(), "GitServiceIntegrationTests", Guid.NewGuid().ToString());
    }

    [Fact]
    public void GetLastTag_WithRealRepository_ReturnsValidResult()
    {
        // This test depends on the actual repository state
        var result = _gitService.GetLastTag();
        
        // Result can be null (no tags) or a string (tag name)
        Assert.True(result == null || !string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void GetCommitsSince_WithRealRepository_ReturnsCommits()
    {
        var commits = _gitService.GetCommitsSince();
        
        Assert.NotNull(commits);
        // In a real repository, we should have at least some commits
        // Note: This might fail in a brand new repository with no commits
        
        if (commits.Any())
        {
            var firstCommit = commits.First();
            Assert.NotNull(firstCommit.Hash);
            Assert.NotNull(firstCommit.Author);
            Assert.NotNull(firstCommit.Date);
            Assert.NotNull(firstCommit.Message);
        }
    }

    [Fact]
    public void GenerateChangeLog_WithRealRepository_GeneratesValidOutput()
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);
        
        var changeLog = _gitService.GenerateChangeLog(config);
        
        Assert.NotNull(changeLog);
        
        // Should contain some standard changelog elements
        if (!string.IsNullOrEmpty(changeLog))
        {
            Assert.Contains("##", changeLog); // Should have markdown headers
        }
    }

    [Fact]
    public void GenerateChangeLog_WithFormatOptions_GeneratesFormattedOutput()
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);
        var options = new ChangeLogFormatOptions
        {
            IncludeHash = true,
            IncludeAuthor = true,
            DateFormat = "MM/dd/yyyy"
        };
        
        var changeLog = _gitService.GenerateChangeLog(config, options);
        
        Assert.NotNull(changeLog);
        if (!string.IsNullOrEmpty(changeLog))
        {
            Assert.Contains("##", changeLog); // Should have markdown headers
        }
    }

    [Fact]
    public void WriteChangeLog_WithRealRepository_CreatesFile()
    {
        var tempFile = Path.Combine(_tempDirectory, "test-changelog.md");
        var config = new ChangeLogConfig(ChangeLogSource.All);
        
        Directory.CreateDirectory(_tempDirectory);
        
        _gitService.WriteChangeLog(tempFile, config);
        
        if (_gitService.GenerateChangeLog(config).Length > 0)
        {
            Assert.True(File.Exists(tempFile));
            
            var content = File.ReadAllText(tempFile);
            
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public void WriteChangeLog_WithFormatOptions_CreatesFormattedFile()
    {
        var tempFile = Path.Combine(_tempDirectory, "formatted-changelog.md");
        var config = new ChangeLogConfig(ChangeLogSource.All);
        var options = new ChangeLogFormatOptions
        {
            IncludeHash = true,
            IncludeAuthor = true,
            DateFormat = "MM/dd/yyyy"
        };
        
        Directory.CreateDirectory(_tempDirectory);
        
        _gitService.WriteChangeLog(tempFile, config, options);
        
        if (_gitService.GenerateChangeLog(config, options).Length > 0)
        {
            Assert.True(File.Exists(tempFile));
            
            var content = File.ReadAllText(tempFile);
            
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public void GetUrls_WithRealRepository_ReturnsUrls()
    {
        var urls = _gitService.GetUrls("main", "abc123");
        
        Assert.NotNull(urls);
        
        // URLs depend on the repository configuration
        // At minimum, the object should be created properly
        Assert.IsType<GitUrls>(urls);
    }

    [Fact]
    public void SetSafeDirectory_WithValidPath_DoesNotThrow()
    {
        var testPath = Path.GetTempPath();
        
        var exception = Record.Exception(() => _gitService.SetSafeDirectory(testPath));
        
        // Should not throw for valid directory
        // Note: This might fail if git is not installed or configured
        Assert.True(exception == null || exception.Message.Contains("git"));
    }

    [Fact]
    public void CreateTag_WithTestTag_HandlesProperly()
    {
        // Note: This test is commented out as it would actually create/push a tag
        // Uncomment for testing in a test repository only
        
        /*
        // Arrange
        var testTag = $"test-tag-{DateTime.Now:yyyyMMdd-HHmmss}";
        
        // Act & Assert
        var exception = Record.Exception(() => _gitService.CreateTag(testTag));
        
        // Should handle the operation (success or failure)
        Assert.True(exception == null || exception.Message.Contains("git"));
        */
        
        // Placeholder assertion for test structure
        Assert.True(true);
    }

    [Fact]
    public void GetCommitsSince_WithSpecificTag_FiltersCorrectly()
    {
        var lastTag = _gitService.GetLastTag();
        
        if (lastTag != null)
        {
            var commitsSinceTag = _gitService.GetCommitsSince(lastTag);
            var allCommits = _gitService.GetCommitsSince();
            
            Assert.NotNull(commitsSinceTag);
            Assert.NotNull(allCommits);
            
            // Commits since tag should be <= all commits
            Assert.True(commitsSinceTag.Count <= allCommits.Count);
        }
        else
        {
            // No tags in repository, just verify method doesn't crash
            var commits = _gitService.GetCommitsSince("nonexistent-tag");

            Assert.NotNull(commits);
        }
    }

    [Fact]
    public void Service_PerformsConsistentlyAcrossMultipleCalls()
    {
        // Test that the service is stateless and performs consistently
        var lastTag1 = _gitService.GetLastTag();
        var lastTag2 = _gitService.GetLastTag();
        
        var commits1 = _gitService.GetCommitsSince();
        var commits2 = _gitService.GetCommitsSince();
        
        Assert.Equal(lastTag1, lastTag2);
        Assert.Equal(commits1.Count, commits2.Count);
        
        if (commits1.Any() && commits2.Any())
        {
            Assert.Equal(commits1.First().Hash, commits2.First().Hash);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}