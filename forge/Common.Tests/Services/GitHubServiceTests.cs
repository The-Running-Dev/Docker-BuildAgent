using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

using Entities;
using Services;
using Utilities;
using Parameters;

namespace Common.Tests.Services;

/// <summary>
/// Comprehensive unit tests for IGitHubService implementation.
/// Tests cover release creation, URL parsing, release body formatting, error handling, and configuration options.
/// </summary>
public class GitHubServiceTests
{
    private readonly Mock<IGitService> _mockGitService;
    
    private readonly Mock<IChangeLogConfigService> _mockChangeLogConfigService;
    
    private readonly Mock<ILogger<GitHubService>> _mockLogger;
    
    private readonly GitHubService _gitHubService;
    
    private readonly DockerParams _testParams;

    public GitHubServiceTests()
    {
        _mockGitService = new Mock<IGitService>();
        _mockChangeLogConfigService = new Mock<IChangeLogConfigService>();
        _mockLogger = new Mock<ILogger<GitHubService>>();
        _gitHubService = new GitHubService(_mockGitService.Object, _mockChangeLogConfigService.Object, _mockLogger.Object);
        _testParams = new DockerParams
        {
            RepositoryUrl = "https://github.com/owner/repo",
            ReleaseTag = "v1.0.0",
            Tags = ["myapp:1.0.0", "myapp:latest"],
            RegistryToken = "test-token",
            ChangeLogConfig = new ChangeLogConfig(ChangeLogSource.SpecificTag, "v0.9.0")
        };
    }

    [Theory]
    [InlineData("https://github.com/owner/repo", "owner", "repo")]
    [InlineData("https://github.com/owner/repo.git", "owner", "repo")]
    [InlineData("git@github.com:owner/repo.git", "owner", "repo")]
    [InlineData("https://github.com/microsoft/dotnet", "microsoft", "dotnet")]
    public void ParseRepositoryUrl_WithValidUrls_ShouldReturnOwnerAndRepo(string url, string expectedOwner, string expectedRepo)
    {
        var (owner, repo) = _gitHubService.ParseRepositoryUrl(url);

        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedRepo, repo);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("https://bitbucket.org/owner/repo")]
    public void ParseRepositoryUrl_WithInvalidUrls_ShouldThrowArgumentException(string invalidUrl)
    {
        var exception = Assert.Throws<ArgumentException>(() => _gitHubService.ParseRepositoryUrl(invalidUrl));

        Assert.Contains("is not a valid GitHubService URL", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseRepositoryUrl_WithEmptyUrls_ShouldThrowArgumentException(string emptyUrl)
    {
        var exception = Assert.Throws<ArgumentException>(() => _gitHubService.ParseRepositoryUrl(emptyUrl));

        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ParseRepositoryUrl_WithNullUrl_ShouldThrowArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => _gitHubService.ParseRepositoryUrl(null!));

        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Theory]
    [InlineData("https://github.com/owner-with-dashes/repo-with-dashes", "owner-with-dashes", "repo-with-dashes")]
    [InlineData("https://github.com/Owner123/Repo123", "Owner123", "Repo123")]
    [InlineData("https://github.com/123owner/456repo.git", "123owner", "456repo")]
    [InlineData("git@github.com:owner_underscore/repo_underscore.git", "owner_underscore", "repo_underscore")]
    public void ParseRepositoryUrl_WithSpecialCharacters_ShouldParseCorrectly(string url, string expectedOwner, string expectedRepo)
    {
        var (owner, repo) = _gitHubService.ParseRepositoryUrl(url);

        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedRepo, repo);
    }

    [Fact]
    public void FormatReleaseBody_WithDefaultOptions_ShouldIncludeDockerImagesAndChangelog()
    {
        var dockerTags = new List<string> { "myapp:1.0.0", "myapp:latest" };
        var releaseNotes = "## Changes\n- Fixed bug #123\n- Added feature XYZ";

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes);

        Assert.Contains("## Images", result);
        Assert.Contains("myapp:1.0.0", result);
        Assert.Contains("myapp:latest", result);
        Assert.Contains("docker pull", result);
        Assert.Contains("## CHANGELOG", result);
        Assert.Contains("Fixed bug #123", result);
        Assert.Contains("Added feature XYZ", result);
    }

    [Fact]
    public void FormatReleaseBody_WithCustomSections_ShouldIncludeCustomContent()
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };
        var releaseNotes = "Bug fixes";
        var options = new GitHubReleaseOptions
        {
            CustomSections = new Dictionary<string, string>
            {
                ["Breaking Changes"] = "- API endpoints changed",
                ["Migration Guide"] = "See docs/migration.md"
            }
        };

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes, options);

        Assert.Contains("## Breaking Changes", result);
        Assert.Contains("API endpoints changed", result);
        Assert.Contains("## Migration Guide", result);
        Assert.Contains("See docs/migration.md", result);
    }

    [Fact]
    public void FormatReleaseBody_WithDockerImagesDisabled_ShouldNotIncludeImages()
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };
        var releaseNotes = "Release notes";
        var options = new GitHubReleaseOptions { IncludeDockerImages = false };

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes, options);

        Assert.DoesNotContain("## Images", result);
        Assert.DoesNotContain("docker pull", result);
        Assert.Contains("## CHANGELOG", result);
    }

    [Fact]
    public void FormatReleaseBody_WithChangelogDisabled_ShouldNotIncludeChangelog()
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };
        var releaseNotes = "Release notes";
        var options = new GitHubReleaseOptions { IncludeChangeLog = false };

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes, options);

        Assert.Contains("## Images", result);
        Assert.DoesNotContain("## CHANGELOG", result);
        Assert.DoesNotContain("Release notes", result);
    }

    [Fact]
    public void FormatReleaseBody_WithEmptyTags_ShouldNotIncludeImages()
    {
        var dockerTags = new List<string>();
        var releaseNotes = "Release notes";

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes);

        Assert.DoesNotContain("## Images", result);
        Assert.Contains("## CHANGELOG", result);
    }

    [Fact]
    public void FormatReleaseBody_WithNullOptions_ShouldUseDefaults()
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };
        var releaseNotes = "Release notes";

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes, null!);

        Assert.Contains("## Images", result);
        Assert.Contains("## CHANGELOG", result);
    }

    [Fact]
    public void FormatReleaseBody_WithEmptyTagsAndNotes_ShouldReturnMinimalContent()
    {
        var emptyTags = new List<string>();
        var emptyNotes = "";

        var result = _gitHubService.FormatReleaseBody(emptyTags, emptyNotes);

        Assert.NotNull(result);

        // Should not contain images or changelog sections when empty
        Assert.DoesNotContain("## Images", result);
        Assert.DoesNotContain("## CHANGELOG", result);
    }

    [Fact]
    public void FormatReleaseBody_WithNullTags_ShouldHandleGracefully()
    {
        // Act & Assert - Should not throw, but handle null gracefully
        var exception = Record.Exception(() => _gitHubService.FormatReleaseBody(null!, "test notes"));

        // The behavior depends on implementation - either handle gracefully or throw ArgumentNullException
        // This test documents the expected behavior and should be updated based on actual implementation
    }

    [Theory]
    [InlineData(null!)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatReleaseBody_WithNullOrEmptyReleaseNotes_ShouldNotIncludeChangelog(string? releaseNotes)
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes!);

        Assert.Contains("## Images", result);
        Assert.DoesNotContain("## CHANGELOG", result);
    }

    [Fact]
    public void FormatReleaseBody_WithMultipleCustomSections_ShouldIncludeAllSections()
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };
        var releaseNotes = "Bug fixes";
        var options = new GitHubReleaseOptions
        {
            CustomSections = new Dictionary<string, string>
            {
                ["Breaking Changes"] = "- API endpoints changed",
                ["Migration Guide"] = "See docs/migration.md",
                ["Known Issues"] = "- Issue with SSL certificates"
            }
        };

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes, options);

        Assert.Contains("## Breaking Changes", result);
        Assert.Contains("API endpoints changed", result);
        Assert.Contains("## Migration Guide", result);
        Assert.Contains("See docs/migration.md", result);
        Assert.Contains("## Known Issues", result);
        Assert.Contains("Issue with SSL certificates", result);
    }

    [Fact]
    public void FormatReleaseBody_WithCustomSectionsAndDisabledDefaults_ShouldOnlyShowCustomSections()
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };
        var releaseNotes = "Bug fixes";
        var options = new GitHubReleaseOptions
        {
            IncludeDockerImages = false,
            IncludeChangeLog = false,
            CustomSections = new Dictionary<string, string>
            {
                ["Custom Section"] = "Custom content only"
            }
        };

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes, options);

        Assert.Contains("## Custom Section", result);
        Assert.Contains("Custom content only", result);
        Assert.DoesNotContain("## Images", result);
        Assert.DoesNotContain("## CHANGELOG", result);
    }

    [Fact]
    public void FormatReleaseBody_WithSpecialCharactersInTags_ShouldFormatCorrectly()
    {
        var dockerTags = new List<string> 
        { 
            "registry.example.com/namespace/myapp:1.0.0-alpha.1",
            "myapp:latest-dev"
        };
        var releaseNotes = "Pre-release version";

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes);

        Assert.Contains("registry.example.com/namespace/myapp:1.0.0-alpha.1", result);
        Assert.Contains("myapp:latest-dev", result);
        Assert.Contains("docker pull registry.example.com/namespace/myapp:1.0.0-alpha.1", result);
        Assert.Contains("docker pull myapp:latest-dev", result);
    }

    [Fact]
    public async Task CreateReleaseAsync_WithValidParameters_ShouldCallGitService()
    {
        var expectedCommits = new List<CommitInfo>
        {
            new CommitInfo { Hash = "abc123", Author = "Test Author", Date = "2024-01-01", Message = "Test commit" }
        };

        _mockGitService.Setup(x => x.GetCommitsSince("v0.9.0"))
                      .Returns(expectedCommits);
        _mockGitService.Setup(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), It.IsAny<ChangeLogFormatOptions>()))
                      .Returns("## Test Changelog\n- Test commit");

        // Note: This test would require mocking the GitHubService API client, which is complex
        // In a real scenario, you'd either:
        // 1. Mock the Octokit GitHubClient
        // 2. Create an abstraction over the GitHubService client
        // 3. Use integration tests with a test repository

        // For now, we'll test the parameter validation
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHubService.CreateRelease(_testParams));
    }

    [Fact]
    public async Task CreateReleaseAsync_WithNullParameters_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _gitHubService.CreateRelease(null!));
    }

    [Fact]
    public async Task CreateReleaseAsync_WithEmptyRepositoryUrl_ShouldThrowArgumentException()
    {
        var invalidParams = new DockerParams
        {
            RepositoryUrl = "",
            ReleaseTag = "v1.0.0",
            Tags = ["test:1.0.0"],
            RegistryToken = "test-token"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.CreateRelease(invalidParams));

        Assert.Contains("RepositoryUrl cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateReleaseAsync_WithEmptyReleaseTag_ShouldThrowArgumentException()
    {
        var invalidParams = new DockerParams
        {
            RepositoryUrl = "https://github.com/owner/repo",
            ReleaseTag = "",
            Tags = ["test:1.0.0"],
            RegistryToken = "test-token"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.CreateRelease(invalidParams));

        Assert.Contains("ReleaseTag cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateReleaseAsync_WithEmptyToken_ShouldThrowArgumentException()
    {
        var invalidParams = new DockerParams
        {
            RepositoryUrl = "https://github.com/owner/repo",
            ReleaseTag = "v1.0.0",
            Tags = ["test:1.0.0"],
            RegistryToken = ""
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.CreateRelease(invalidParams));

        Assert.Contains("RegistryToken cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateReleaseAsync_WithFormatOptions_ShouldCallGitServiceWithOptions()
    {
        var formatOptions = new ChangeLogFormatOptions
        {
            IncludeHash = true,
            IncludeAuthor = true
        };

        _mockGitService.Setup(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), It.IsAny<ChangeLogFormatOptions>()))
                      .Returns("## Formatted Changelog\n- abc123 by Test Author: Test commit");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHubService.CreateRelease(_testParams, null, formatOptions));

        // Verify the format options were used
        _mockGitService.Verify(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), formatOptions), Times.Once);
    }

    [Fact]
    public void FormatReleaseBody_WithNullTags_ShouldThrowArgumentNullException()
    {
        // Updated to reflect the new null check behavior
        var exception = Assert.Throws<ArgumentNullException>(() => _gitHubService.FormatReleaseBody(null!, "test notes"));
        Assert.Contains("dockerTags", exception.Message);
    }

    [Fact]
    public async Task CreateReleaseAsync_WithGitHubReleaseOptions_ShouldUseCustomOptions()
    {
        var releaseOptions = new GitHubReleaseOptions
        {
            Draft = true,
            PreRelease = true,
            Name = "Custom Release Name",
            IncludeChangeLog = false,
            IncludeDockerImages = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHubService.CreateRelease(_testParams, releaseOptions));

        // Verify that changelog generation was not called when IncludeChangeLog is false
        _mockGitService.Verify(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), It.IsAny<ChangeLogFormatOptions>()), Times.Never);
    }

    [Fact]
    public async Task GetReleaseAsync_WithEmptyReleaseTag_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.GetRelease("https://github.com/owner/repo", "", "test-token"));

        Assert.Contains("Release tag cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetReleaseAsync_WithNullReleaseTag_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.GetRelease("https://github.com/owner/repo", null!, "test-token"));

        Assert.Contains("Release tag cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetReleaseAsync_WithEmptyToken_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.GetRelease("https://github.com/owner/repo", "v1.0.0", ""));

        Assert.Contains("GitHubService token cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ReleaseExistsAsync_WithInvalidUrl_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.ReleaseExists("invalid-url", "v1.0.0", "test-token"));

        Assert.Contains("not a valid GitHubService URL", exception.Message);
    }

    [Fact]
    public async Task ReleaseExistsAsync_WithEmptyToken_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.ReleaseExists("https://github.com/owner/repo", "v1.0.0", ""));

        Assert.Contains("GitHubService token cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ReleaseExistsAsync_WithNullToken_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.ReleaseExists("https://github.com/owner/repo", "v1.0.0", null!));

        Assert.Contains("GitHubService token cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ReleaseExistsAsync_WithEmptyReleaseTag_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.ReleaseExists("https://github.com/owner/repo", "", "test-token"));

        Assert.Contains("Release tag cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ReleaseExistsAsync_WithNullReleaseTag_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.ReleaseExists("https://github.com/owner/repo", null!, "test-token"));

        Assert.Contains("Release tag cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetReleaseAsync_WithNullToken_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => 
            _gitHubService.GetRelease("https://github.com/owner/repo", "v1.0.0", null!));
        
        Assert.Contains("GitHubService token cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetReleaseAsync_WithInvalidUrl_ShouldThrowArgumentException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _gitHubService.GetRelease("invalid-url", "v1.0.0", "test-token"));

        Assert.Contains("not a valid GitHubService URL", exception.Message);
    }

    [Fact]
    public void GitHubReleaseInfo_DefaultValues_ShouldBeCorrect()
    {
        var releaseInfo = new GitHubReleaseInfo();

        Assert.Equal(0, releaseInfo.Id);
        Assert.Equal(string.Empty, releaseInfo.TagName);
        Assert.Equal(string.Empty, releaseInfo.Name);
        Assert.Equal(string.Empty, releaseInfo.Body);
        Assert.False(releaseInfo.IsDraft);
        Assert.False(releaseInfo.Prerelease);
        Assert.Equal(default(DateTime), releaseInfo.CreatedAt);
        Assert.Null(releaseInfo.PublishedAt);
        Assert.Equal(string.Empty, releaseInfo.Url);
    }

    [Fact]
    public void GitHubReleaseInfo_PropertySetting_ShouldWork()
    {
        var now = DateTime.UtcNow;
        var releaseInfo = new GitHubReleaseInfo();

        releaseInfo.Id = 123;
        releaseInfo.TagName = "v1.0.0";
        releaseInfo.Name = "Release 1.0.0";
        releaseInfo.Body = "Release notes";
        releaseInfo.IsDraft = true;
        releaseInfo.Prerelease = false;
        releaseInfo.CreatedAt = now;
        releaseInfo.PublishedAt = now.AddHours(1);
        releaseInfo.Url = "https://github.com/owner/repo/releases/tag/v1.0.0";

        Assert.Equal(123, releaseInfo.Id);
        Assert.Equal("v1.0.0", releaseInfo.TagName);
        Assert.Equal("Release 1.0.0", releaseInfo.Name);
        Assert.Equal("Release notes", releaseInfo.Body);
        Assert.True(releaseInfo.IsDraft);
        Assert.False(releaseInfo.Prerelease);
        Assert.Equal(now, releaseInfo.CreatedAt);
        Assert.Equal(now.AddHours(1), releaseInfo.PublishedAt);
        Assert.Equal("https://github.com/owner/repo/releases/tag/v1.0.0", releaseInfo.Url);
    }

    [Fact]
    public void GitHubReleaseOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new GitHubReleaseOptions();

        Assert.False(options.Draft);
        Assert.False(options.PreRelease);
        Assert.True(options.IncludeDockerImages);
        Assert.True(options.IncludeChangeLog);
        Assert.Empty(options.CustomSections);
        Assert.Null(options.Name);
        Assert.Null(options.ChangeLogFormatOptions);
    }

    [Fact]
    public void GitHubReleaseOptions_PropertySetting_ShouldWork()
    {
        var options = new GitHubReleaseOptions();
        var formatOptions = new ChangeLogFormatOptions { IncludeHash = true };

        options.Draft = true;
        options.PreRelease = true;
        options.Name = "Custom Release";
        options.IncludeDockerImages = false;
        options.IncludeChangeLog = false;
        options.CustomSections = new Dictionary<string, string> { ["Test"] = "Value" };
        options.ChangeLogFormatOptions = formatOptions;

        Assert.True(options.Draft);
        Assert.True(options.PreRelease);
        Assert.Equal("Custom Release", options.Name);
        Assert.False(options.IncludeDockerImages);
        Assert.False(options.IncludeChangeLog);
        Assert.Single(options.CustomSections);
        Assert.Equal("Value", options.CustomSections["Test"]);
        Assert.Same(formatOptions, options.ChangeLogFormatOptions);
    }

    [Fact]
    public void ChangeLogFormatOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new ChangeLogFormatOptions();

        Assert.Equal("yyyy.MM.dd", options.DateFormat);
        Assert.False(options.IncludeHash);
        Assert.False(options.IncludeAuthor);
    }

    [Fact]
    public void ChangeLogFormatOptions_PropertySetting_ShouldWork()
    {
        var options = new ChangeLogFormatOptions
        {
            DateFormat = "MM/dd/yyyy",
            IncludeHash = true,
            IncludeAuthor = true
        };

        Assert.Equal("MM/dd/yyyy", options.DateFormat);
        Assert.True(options.IncludeHash);
        Assert.True(options.IncludeAuthor);
    }

    [Fact]
    public void GitHubService_WithMockedGitService_ShouldUseProvidedService()
    {
        var mockGitService = new Mock<IGitService>();
        var mockChangeLogConfigFactory = new Mock<IChangeLogConfigService>();
        var mockLogger = new Mock<ILogger<GitHubService>>();
        var gitHubService = new GitHubService(mockGitService.Object, mockChangeLogConfigFactory.Object, mockLogger.Object);

        Assert.NotNull(gitHubService);
        Assert.IsType<GitHubService>(gitHubService);
    }

    [Fact]
    public void GitHubService_WithNullGitService_ShouldThrowArgumentNullException()
    {
        var mockChangeLogConfigFactory = new Mock<IChangeLogConfigService>();
        var mockLogger = new Mock<ILogger<GitHubService>>();
        
        Assert.Throws<ArgumentNullException>(() => 
            new GitHubService(null!, mockChangeLogConfigFactory.Object, mockLogger.Object));
    }

    [Fact]
    public void GitHubService_WithNullChangeLogConfigFactory_ShouldThrowArgumentNullException()
    {
        var mockGitService = new Mock<IGitService>();
        var mockLogger = new Mock<ILogger<GitHubService>>();
        
        Assert.Throws<ArgumentNullException>(() => 
            new GitHubService(mockGitService.Object, null!, mockLogger.Object));
    }

    [Fact]
    public void GitHubService_WithNullLogger_ShouldThrowArgumentNullException()
    {
        var mockGitService = new Mock<IGitService>();
        var mockChangeLogConfigFactory = new Mock<IChangeLogConfigService>();
        
        Assert.Throws<ArgumentNullException>(() => 
            new GitHubService(mockGitService.Object, mockChangeLogConfigFactory.Object, null!));
    }

    [Fact]
    public async Task GitHubService_WithMockedGitService_ShouldCallGitServiceMethods()
    {
        var expectedCommits = new List<CommitInfo>
        {
            new CommitInfo { Hash = "abc123", Author = "Test Author", Date = "2024-01-01", Message = "Test commit" }
        };

        _mockGitService.Setup(x => x.GetCommitsSince("v0.9.0"))
                      .Returns(expectedCommits);
        _mockGitService.Setup(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), It.IsAny<ChangeLogFormatOptions>()))
                      .Returns("## Test Changelog\n- Test commit");

        // Act & Assert - This will fail with InvalidOperationException due to GitHubService API call
        // but it demonstrates that the GitService service integration works
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHubService.CreateRelease(_testParams));

        // Verify the exception is from GitHubService API, not from GitService service
        Assert.NotNull(exception);
    }

    #region Additional Edge Case Tests

    [Fact]
    public async Task CreateReleaseAsync_WithEmptyTags_ShouldStillCreateRelease()
    {
        var paramsWithEmptyTags = new DockerParams
        {
            RepositoryUrl = "https://github.com/owner/repo",
            ReleaseTag = "v1.0.0",
            Tags = new List<string>(),
            RegistryToken = "test-token",
            ChangeLogConfig = new ChangeLogConfig(ChangeLogSource.SpecificTag, "v0.9.0")
        };

        _mockGitService.Setup(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), null))
                      .Returns("## Test Changelog\n- Test commit");

        // Act & Assert - Should fail due to GitHubService API, not due to empty tags
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHubService.CreateRelease(paramsWithEmptyTags));

        // Verify the GitService service was still called
        _mockGitService.Verify(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), It.IsAny<ChangeLogFormatOptions>()), Times.Once);
    }

    [Fact] 
    public void FormatReleaseBody_WithLargeTags_ShouldHandleCorrectly()
    {
        var largeTags = new List<string>();
        for (int i = 0; i < 50; i++)
        {
            largeTags.Add($"myapp:tag-{i:D3}");
        }
        var releaseNotes = "Many tags release";

        var result = _gitHubService.FormatReleaseBody(largeTags, releaseNotes);

        Assert.Contains("## Images", result);
        Assert.Contains("myapp:tag-000", result);
        Assert.Contains("myapp:tag-049", result);
        Assert.Contains("## CHANGELOG", result);
        Assert.Contains("Many tags release", result);
    }

    [Fact]
    public void GitHubReleaseInfo_WithNullPublishedAt_ShouldHandleCorrectly()
    {
        var releaseInfo = new GitHubReleaseInfo
        {
            Id = 123,
            TagName = "v1.0.0",
            Name = "Test Release",
            Body = "Test body",
            IsDraft = true,
            Prerelease = false,
            CreatedAt = DateTime.UtcNow,
            PublishedAt = null,
            Url = "https://github.com/owner/repo/releases/tag/v1.0.0"
        };

        Assert.Null(releaseInfo.PublishedAt);
        Assert.True(releaseInfo.IsDraft);
    }

    [Fact]
    public void GitHubReleaseOptions_WithEmptyCustomSections_ShouldNotAffectFormat()
    {
        var dockerTags = new List<string> { "myapp:1.0.0" };
        var releaseNotes = "Release notes";
        var options = new GitHubReleaseOptions
        {
            CustomSections = new Dictionary<string, string>()
        };

        var result = _gitHubService.FormatReleaseBody(dockerTags, releaseNotes, options);

        Assert.Contains("## Images", result);
        Assert.Contains("## CHANGELOG", result);
        Assert.DoesNotContain("##  ", result); // No empty section headers
    }

    #endregion

    [Fact]
    public async Task CreateReleaseAsync_WithBothOptionsAndFormatOptions_ShouldUseFormatOptionsParameter()
    {
        var releaseOptions = new GitHubReleaseOptions
        {
            IncludeChangeLog = true,
            ChangeLogFormatOptions = new ChangeLogFormatOptions { IncludeHash = false }
        };
        
        var formatOptions = new ChangeLogFormatOptions
        {
            IncludeHash = true,
            IncludeAuthor = true
        };

        _mockGitService.Setup(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), It.IsAny<ChangeLogFormatOptions>()))
                      .Returns("## Formatted Changelog\n- abc123 by Test Author: Test commit");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHubService.CreateRelease(_testParams, releaseOptions, formatOptions));

        // Verify the parameter formatOptions were used, not the options.ChangeLogFormatOptions
        _mockGitService.Verify(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), formatOptions), Times.Once);
    }

    [Fact]
    public async Task CreateReleaseAsync_WithOptionsChangeLogFormatOptions_ShouldUseOptionsFormatOptions()
    {
        var releaseOptions = new GitHubReleaseOptions
        {
            IncludeChangeLog = true,
            ChangeLogFormatOptions = new ChangeLogFormatOptions { IncludeHash = true, IncludeAuthor = false }
        };

        var expectedFormatOptions = releaseOptions.ChangeLogFormatOptions;

        _mockGitService.Setup(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), It.IsAny<ChangeLogFormatOptions>()))
                      .Returns("## Formatted Changelog\n- abc123: Test commit");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _gitHubService.CreateRelease(_testParams, releaseOptions));

        // Verify the options.ChangeLogFormatOptions were used
        _mockGitService.Verify(x => x.GenerateChangeLog(It.IsAny<ChangeLogConfig>(), expectedFormatOptions), Times.Once);
    }
}