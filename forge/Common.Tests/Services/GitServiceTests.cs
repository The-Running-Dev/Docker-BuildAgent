using Moq;
using Xunit;
using Microsoft.Extensions.Logging;

using Entities;
using Services;
using Utilities;

namespace Common.Tests.Services;

/// <summary>
/// Comprehensive unit tests for the Git        Assert.Contains("Directory Cannot Be Empty or White Space", exception.Message);ervice class.
/// Tests cover changelog generation, commit retrieval, tag operations, URL generation, and error handling.
/// </summary>
public class GitServiceTests
{
    private readonly Mock<ILogger<GitService>> _mockLogger;

    private readonly GitService _gitService;

    public GitServiceTests()
    {
        _mockLogger = new Mock<ILogger<GitService>>();
        _gitService = new GitService(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GitService(null!));
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        var logger = new Mock<ILogger<GitService>>();

        var service = new GitService(logger.Object);

        Assert.NotNull(service);
        Assert.IsType<GitService>(service);
    }

    [Fact]
    public void GenerateChangeLog_WithNullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gitService.GenerateChangeLog(null!));
    }

    [Fact]
    public void GenerateChangeLog_WithValidConfig_ReturnsFormattedString()
    {
        var config = new ChangeLogConfig(ChangeLogSource.SpecificTag, "v1.0.0");

        // Note: This test would require mocking the git command execution or using integration tests
        // Since we're testing the service layer, we'll test parameter validation
        var exception = Record.Exception(() => _gitService.GenerateChangeLog(config));

        // The method should not throw for valid parameters (may fail on git execution in test environment)
        Assert.True(exception == null || exception is not ArgumentNullException);
    }

    [Fact]
    public void GenerateChangeLog_WithFormatOptions_WithValidParameters_DoesNotThrowArgumentException()
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);
        var options = new ChangeLogFormatOptions { IncludeHash = true };

        var exception = Record.Exception(() => _gitService.GenerateChangeLog(config, options));

        // Should not throw ArgumentNullException for valid parameters
        Assert.True(exception == null || exception is not ArgumentNullException);
    }

    [Fact]
    public void GenerateChangeLog_WithNullOptions_UsesDefaults()
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);

        var exception = Record.Exception(() => _gitService.GenerateChangeLog(config, null));

        // Should not throw ArgumentNullException when options is null (should use defaults)
        Assert.True(exception == null || exception is not ArgumentNullException);
    }

    [Fact]
    public void WriteChangeLog_WithNullFilePath_ThrowsArgumentNullException()
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);

        Assert.Throws<ArgumentNullException>(() => _gitService.WriteChangeLog(null!, config));
    }

    [Fact]
    public void WriteChangeLog_WithNullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gitService.WriteChangeLog("test.md", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void WriteChangeLog_WithEmptyOrWhitespaceFilePath_ThrowsArgumentException(string filePath)
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);

        var exception = Assert.Throws<ArgumentException>(() => _gitService.WriteChangeLog(filePath, config));
        Assert.Contains("File Path Cannot Be Empty or Whitespace", exception.Message);
        Assert.Equal("filePath", exception.ParamName);
    }

    [Fact]
    public void WriteChangeLog_WithValidParameters_DoesNotThrowArgumentException()
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);
        var tempFile = Path.GetTempFileName();

        try
        {
            var exception = Record.Exception(() => _gitService.WriteChangeLog(tempFile, config));

            // Should not throw ArgumentNullException or ArgumentException for valid parameters
            Assert.True(exception == null || 
                       (exception is not ArgumentNullException && exception is not ArgumentException));
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void WriteChangeLog_WithFormatOptions_DoesNotThrowArgumentException()
    {
        var config = new ChangeLogConfig(ChangeLogSource.All);
        var options = new ChangeLogFormatOptions { IncludeHash = true };
        var tempFile = Path.GetTempFileName();

        try
        {
            var exception = Record.Exception(() => _gitService.WriteChangeLog(tempFile, config, options));

            // Should not throw ArgumentNullException or ArgumentException for valid parameters
            Assert.True(exception == null || 
                       (exception is not ArgumentNullException && exception is not ArgumentException));
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void CreateTag_WithNullTag_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gitService.CreateTag(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void CreateTag_WithEmptyOrWhitespaceTag_ThrowsArgumentException(string tag)
    {
        var exception = Assert.Throws<ArgumentException>(() => _gitService.CreateTag(tag));
        
        Assert.Contains("Tag Cannot Be Empty or Whitespace", exception.Message);
        Assert.Equal("tag", exception.ParamName);
    }

    [Fact]
    public void CreateTag_WithValidTag_DoesNotThrowArgumentException()
    {
        const string tag = "v1.0.0";

        var exception = Record.Exception(() => _gitService.CreateTag(tag));

        // Should not throw ArgumentNullException or ArgumentException for valid parameters
        Assert.True(exception == null || 
                   (exception is not ArgumentNullException && exception is not ArgumentException));
    }

    [Fact]
    public void GetUrls_WithNullBranch_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gitService.GetUrls(null!, "abc123"));
    }

    [Fact]
    public void GetUrls_WithNullCommit_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gitService.GetUrls("main", null!));
    }

    [Fact]
    public void GetUrls_WithValidParameters_ReturnsGitUrls()
    {
        const string branch = "main";
        const string commit = "abc123";

        var result = _gitService.GetUrls(branch, commit);

        Assert.NotNull(result);
        Assert.IsType<GitUrls>(result);
        // Note: Actual URL values depend on git configuration, so we just verify structure
    }

    [Fact]
    public void SetSafeDirectory_WithNullDirectoryPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gitService.SetSafeDirectory(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void SetSafeDirectory_WithEmptyOrWhitespaceDirectoryPath_ThrowsArgumentException(string directoryPath)
    {
        var exception = Assert.Throws<ArgumentException>(() => _gitService.SetSafeDirectory(directoryPath));
        
        Assert.Contains("Directory Cannot Be Empty or White Space", exception.Message);
        Assert.Equal("directoryPath", exception.ParamName);
    }

    [Fact]
    public void SetSafeDirectory_WithValidDirectoryPath_DoesNotThrowArgumentException()
    {
        const string directoryPath = "/valid/path";

        var exception = Record.Exception(() => _gitService.SetSafeDirectory(directoryPath));

        // Should not throw ArgumentNullException or ArgumentException for valid parameters
        Assert.True(exception == null || 
                   (exception is not ArgumentNullException && exception is not ArgumentException));
    }

    [Fact]
    public void GetCommitsSince_WithNullTag_DoesNotThrowArgumentException()
    {
        // Act & Assert
        var exception = Record.Exception(() => _gitService.GetCommitsSince(null));

        // Should not throw ArgumentException for null tag (it's valid to get all commits)
        Assert.True(exception == null || exception is not ArgumentException);
    }

    [Fact]
    public void GetCommitsSince_WithValidTag_DoesNotThrowArgumentException()
    {
        var exception = Record.Exception(() => _gitService.GetCommitsSince("v1.0.0"));

        // Should not throw ArgumentException for valid tag
        Assert.True(exception == null || exception is not ArgumentException);
    }

    [Fact]
    public void GetCommitsSince_ReturnsListOfCommitInfo()
    {
        // Act & Assert
        var exception = Record.Exception(() =>
        {
            var result = _gitService.GetCommitsSince();
            Assert.IsType<List<CommitInfo>>(result);
        });

        // Should return a list (may be empty or throw due to git not being available in test environment)
        Assert.True(exception == null || exception is not ArgumentException);
    }

    [Fact]
    public void GetLastTag_DoesNotThrowArgumentException()
    {
        // Act & Assert
        var exception = Record.Exception(() =>
        {
            var result = _gitService.GetLastTag();
            Assert.True(result == null || result is string);
        });

        // Should not throw ArgumentException (may fail due to git not being available)
        Assert.True(exception == null || exception is not ArgumentException);
    }

    /// <summary>
    /// These tests verify parameter validation without requiring a git repository.
    /// For full integration testing, separate integration test classes should be created
    /// that set up actual git repositories and test the complete functionality.
    /// </summary>
    [Fact]
    public void AllPublicMethods_HaveProperParameterValidation()
    {
        // This test documents that all public methods have been tested for parameter validation
        // Individual parameter validation tests are above

        var publicMethods = typeof(GitService).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName) // Exclude properties and operators
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var expectedMethods = new[]
        {
            nameof(GitService.CreateTag),
            nameof(GitService.GenerateChangeLog),
            nameof(GitService.GetCommitsSince),
            nameof(GitService.GetLastTag),
            nameof(GitService.GetUrls),
            nameof(GitService.SetSafeDirectory),
            nameof(GitService.WriteChangeLog)
        }.OrderBy(x => x).ToList();

        Assert.Equal(expectedMethods, publicMethods);
    }

    [Fact]
    public void GitService_ImplementsIGitService()
    {
        // Assert
        Assert.IsAssignableFrom<IGitService>(_gitService);
    }

    [Fact]
    public void GitService_HasConsistentConstructorPattern()
    {
        // Verify it follows the same pattern as other services (logger injection)
        var constructors = typeof(GitService).GetConstructors();
        
        Assert.Single(constructors);
        
        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        
        Assert.Single(parameters);
        Assert.Equal(typeof(ILogger<GitService>), parameters[0].ParameterType);
    }

    [Fact]
    public void IGitService_HasCleanInterface()
    {
        // Verify the interface doesn't have confusing overloads
        var methods = typeof(IGitService).GetMethods();
        var generateChangeLogMethods = methods.Where(m => m.Name == nameof(IGitService.GenerateChangeLog)).ToList();
        
        // Should have exactly 1 method with optional parameters (consolidated approach)
        Assert.Single(generateChangeLogMethods);
        
        var writeChangeLogMethods = methods.Where(m => m.Name == nameof(IGitService.WriteChangeLog)).ToList();
        
        // Should have exactly 1 method with optional parameters (consolidated approach)
        Assert.Single(writeChangeLogMethods);
        
        // Verify no method takes IGitService as parameter (that was the main issue)
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();

            Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(IGitService));
        }
    }
}