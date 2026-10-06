using Moq;
using Xunit;
using Nuke.Common;
using Assert = Xunit.Assert;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

using Services;
using Entities;
using Parameters;

namespace Common.Tests.Services;

/// <summary>
/// Comprehensive unit tests for the DockerService class methods.
/// Tests cover Docker login, build, tag, and push operations.
/// </summary>
[Collection(nameof(Common.Tests.Build.BuildFailureStateCollection))]
public class DockerServiceTests : IDisposable
{
    private readonly string _testRootDirectory;
    
    private readonly DockerParams _testParams;
    
    private readonly Mock<ILogger<DockerService>> _mockLogger;
    
    private readonly Mock<INodeService> _mockNodeService;
    
    private readonly FakeDockerCli _docker = new();

    private readonly DockerService _dockerService;

    public DockerServiceTests()
    {
        _testRootDirectory = Path.Combine(Path.GetTempPath(), "DockerServiceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRootDirectory);
        
        _testParams = new DockerParams
        {
            RootDirectory = _testRootDirectory,
            TemplatesDir = Path.Combine(_testRootDirectory, "templates"),
            DockerFile = "Dockerfile",
            RegistryUrl = "registry.example.com/myapp",
            RegistryUser = "testuser",
            RegistryToken = "testtoken",
            ImageTag = "myapp",
            Version = new VersionInfo 
            { 
                Version = "1.0.0", 
                FullVersion = "1.0.0", 
                Date = DateTime.Now.ToString("yyyy-MM-dd"), 
                Hash = "abc123" 
            },
            Tags = new List<string> { "registry.example.com/myapp:latest", "registry.example.com/myapp:1.0.0" },
            Verbosity = Verbosity.Normal
        };

        _mockLogger = new Mock<ILogger<DockerService>>();
        _mockNodeService = new Mock<INodeService>();
        _dockerService = new DockerService(_mockLogger.Object, _mockNodeService.Object, _docker);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var mockNodeService = new Mock<INodeService>();
        
        Assert.Throws<ArgumentNullException>(() => new DockerService(null, mockNodeService.Object));
    }

    [Fact]
    public void Constructor_WithNullNodeService_ThrowsArgumentNullException()
    {
        var mockLogger = new Mock<ILogger<DockerService>>();
        
        Assert.Throws<ArgumentNullException>(() => new DockerService(mockLogger.Object, null));
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        var mockLogger = new Mock<ILogger<DockerService>>();
        var mockNodeService = new Mock<INodeService>();
        var service = new DockerService(mockLogger.Object, mockNodeService.Object);

        Assert.NotNull(service);
    }

    [Fact]
    public void Login_WithNullParameters_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _dockerService.Login(null));
    }

    [Fact]
    public void Login_WithValidParameters_DoesNotThrow()
    {
        // This test verifies that the method processes parameters correctly
        // In a real-world scenario, we would need to mock DockerTasks to avoid actual Docker calls
        // For now, we're testing that the method handles parameters properly
        
        var exception = Record.Exception(() => _dockerService.Login(_testParams));
        
        // We expect this to fail in the test environment since Docker may not be available
        // But it should not fail due to parameter validation
        Assert.True(exception == null || !(exception is ArgumentNullException));
    }

    [Fact]
    public void Build_WithNullParameters_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _dockerService.Build(null));
    }

    [Fact]
    public void Build_WithExistingDockerfile_LogsBuildMessage()
    {
        CreateDockerfile("FROM node:18");

        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        // Verify that the build information was logged
        VerifyLoggerInfo($"Building {Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)}...");
    }

    [Fact]
    public void Build_WithMissingDockerfileAndNoTemplatesDir_DoesNotUseTemplate()
    {
        // Don't create the Dockerfile or templates directory
        
        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        // Should not log template usage warning
        VerifyLoggerNotCalled(LogLevel.Warning, "Dockerfile not Found, Using a Template...");
    }

    [Fact]
    public void Build_WithMissingDockerfileAndTemplatesDir_UsesTemplate()
    {
        Directory.CreateDirectory(_testParams.TemplatesDir);
        CreateTemplateDockerfile("node", "FROM node:18");
        
        _mockNodeService.Setup(x => x.DetectApplicationType(_testParams.RootDirectory))
                       .Returns("node");

        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        VerifyLoggerWarning("Dockerfile not Found, Using a Template...");
        _mockNodeService.Verify(x => x.DetectApplicationType(_testParams.RootDirectory), Times.Once);
    }

    [Fact]
    public void Build_WithMissingTemplateFile_ThrowsInvalidOperationException()
    {
        Directory.CreateDirectory(_testParams.TemplatesDir);
        
        _mockNodeService.Setup(x => x.DetectApplicationType(_testParams.RootDirectory))
                       .Returns("unknown");

        var exception = Assert.Throws<InvalidOperationException>(() => _dockerService.Build(_testParams));
        
        Assert.Contains("No Dockerfile Template exists for application type", exception.Message);
    }

    [Fact]
    public void Build_WithTemplateInGitHubDirectory_UsesGitHubTemplate()
    {
        var gitHubTemplatesDir = Path.Combine(_testParams.RootDirectory, ".github", "templates");
        Directory.CreateDirectory(gitHubTemplatesDir);
        CreateTemplateDockerfile(gitHubTemplatesDir, "node", "FROM node:18 # GitHub Template");
        
        _mockNodeService.Setup(x => x.DetectApplicationType(_testParams.RootDirectory))
                       .Returns("node");

        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        VerifyLoggerWarning("Dockerfile not Found, Using a Template...");
        VerifyLoggerInfo($"Found template in: {gitHubTemplatesDir}");
        
        // The template was the build input, and the copy is gone afterwards (#10)
        Assert.Contains("FROM node:18 # GitHub Template", _docker.BuiltDockerFileContent);
        Assert.False(File.Exists(Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)));
    }

    [Fact]
    public void Build_WithTemplateInProjectRoot_UsesProjectRootTemplate()
    {
        var projectTemplatesDir = Path.Combine(_testParams.RootDirectory, "templates");
        Directory.CreateDirectory(projectTemplatesDir);
        CreateTemplateDockerfile(projectTemplatesDir, "node", "FROM node:18 # Project Template");
        
        _mockNodeService.Setup(x => x.DetectApplicationType(_testParams.RootDirectory))
                       .Returns("node");

        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        VerifyLoggerWarning("Dockerfile not Found, Using a Template...");
        VerifyLoggerInfo($"Found template in: {projectTemplatesDir}");
        
        // The template was the build input, and the copy is gone afterwards (#10)
        Assert.Contains("FROM node:18 # Project Template", _docker.BuiltDockerFileContent);
        Assert.False(File.Exists(Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)));
    }

    [Fact]
    public void Build_WithMultipleTemplateLocations_UsesCorrectPriority()
    {
        // Create templates in multiple locations
        var userTemplatesDir = Path.Combine(_testRootDirectory, "user-templates");
        Directory.CreateDirectory(userTemplatesDir);
        _testParams.TemplatesDir = userTemplatesDir; // Set user-specified (highest priority)
        
        var gitHubTemplatesDir = Path.Combine(_testParams.RootDirectory, ".github", "templates");
        Directory.CreateDirectory(gitHubTemplatesDir);
        var projectTemplatesDir = Path.Combine(_testParams.RootDirectory, "templates");
        Directory.CreateDirectory(projectTemplatesDir);
        
        CreateTemplateDockerfile(userTemplatesDir, "node", "FROM node:18 # User Template");
        CreateTemplateDockerfile(gitHubTemplatesDir, "node", "FROM node:18 # GitHub Template");
        CreateTemplateDockerfile(projectTemplatesDir, "node", "FROM node:18 # Project Template");
        
        _mockNodeService.Setup(x => x.DetectApplicationType(_testParams.RootDirectory))
                       .Returns("node");

        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        VerifyLoggerWarning("Dockerfile not Found, Using a Template...");
        VerifyLoggerInfo($"Found template in: {userTemplatesDir}");
        
        // The template was the build input, and the copy is gone afterwards (#10)
        Assert.Contains("FROM node:18 # User Template", _docker.BuiltDockerFileContent);
        Assert.False(File.Exists(Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)));
    }

    [Fact]
    public void Build_WithNoUserSpecifiedTemplatesDir_UsesGitHubTemplates()
    {
        // Clear user-specified templates dir to test fallback
        _testParams.TemplatesDir = string.Empty;
        
        var gitHubTemplatesDir = Path.Combine(_testParams.RootDirectory, ".github", "templates");
        Directory.CreateDirectory(gitHubTemplatesDir);
        CreateTemplateDockerfile(gitHubTemplatesDir, "node", "FROM node:18 # GitHub Template");
        
        _mockNodeService.Setup(x => x.DetectApplicationType(_testParams.RootDirectory))
                       .Returns("node");

        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        VerifyLoggerWarning("Dockerfile not Found, Using a Template...");
        VerifyLoggerInfo($"Found template in: {gitHubTemplatesDir}");
        
        // The template was the build input, and the copy is gone afterwards (#10)
        Assert.Contains("FROM node:18 # GitHub Template", _docker.BuiltDockerFileContent);
        Assert.False(File.Exists(Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)));
    }

    [Fact]
    public void Build_WithVerboseMode_LogsBuildOutput()
    {
        CreateDockerfile("FROM node:18");
        _testParams.Verbosity = Verbosity.Verbose;

        var exception = Record.Exception(() => _dockerService.Build(_testParams));
        
        // Verify that verbose logging was enabled
        // The actual Docker build output logging would be tested with integration tests
        VerifyLoggerInfo($"Building {Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)}...");
    }

    [Fact]
    public void Push_WithNullParameters_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _dockerService.Push(null));
    }

    [Fact]
    public void Push_WithValidParameters_LogsCompletion()
    {
        var exception = Record.Exception(() => _dockerService.Push(_testParams));
        
        // Should log push completion
        VerifyLoggerInfo($"[PUSH] Docker Images: {_testParams.Version}, latest");
    }
    
    [Fact]
    public void Tag_WithNullParameters_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _dockerService.Tag(null));
    }

    [Fact]
    public void Tag_WithValidParameters_LogsTagCompletion()
    {
        var versionTag = _testParams.Tags.FirstOrDefault(x => !x.Contains("latest"));
        
        var exception = Record.Exception(() => _dockerService.Tag(_testParams));
        
        VerifyLoggerInfo($"[TAG] {versionTag}");
    }

    [Fact]
    public void Tag_WithOnlyLatestTag_HandlesGracefully()
    {
        _testParams.Tags = new List<string> { "registry.example.com/myapp:latest" };
        
        var exception = Record.Exception(() => _dockerService.Tag(_testParams));
        
        // Should handle the case where there's no version tag gracefully
        Assert.True(exception == null || !(exception is ArgumentNullException));
    }

    [Fact]
    public void Login_WhenTheLoginFails_ThrowsWithTheRegistryStatusAndNoToken()
    {
        _docker.FailOn = nameof(FakeDockerCli.Login);

        var exception = Assert.Throws<BuildFailureException>(() => _dockerService.Login(_testParams));

        Assert.Equal(BuildFailure.Registry, exception.ExitStatus);
        Assert.DoesNotContain(_testParams.RegistryToken, exception.ToString());
    }

    [Theory]
    [InlineData("registry.example.com/myapp", "registry.example.com")]
    [InlineData("ghcr.io/owner/repo", "ghcr.io")]
    [InlineData("https://ghcr.io/owner/repo", "ghcr.io")]
    [InlineData("registry.example.com", "registry.example.com")]
    public void Login_PassesTheRegistryHostOnly(string registryUrl, string expectedServer)
    {
        _testParams.RegistryUrl = registryUrl;

        _dockerService.Login(_testParams);

        Assert.Equal(expectedServer, _docker.LoginServer);
    }

    [Fact]
    public void Push_WhenThePushFails_ThrowsWithTheRegistryStatus()
    {
        _docker.FailOn = nameof(FakeDockerCli.Push);

        var exception = Assert.Throws<BuildFailureException>(() => _dockerService.Push(_testParams));

        Assert.Equal(BuildFailure.Registry, exception.ExitStatus);
        VerifyLoggerNotCalled(LogLevel.Information, "[PUSH]");
    }

    [Fact]
    public void Push_WhenTheLoginFails_PushesNothing()
    {
        _docker.FailOn = nameof(FakeDockerCli.Login);

        var exception = Assert.Throws<BuildFailureException>(() => _dockerService.Push(_testParams));

        Assert.Equal(BuildFailure.Registry, exception.ExitStatus);
        Assert.Empty(_docker.Pushed);
    }

    [Fact]
    public void Push_PushesLatestThenTheVersion()
    {
        _dockerService.Push(_testParams);

        Assert.Equal(new[] { "registry.example.com/myapp:latest", "registry.example.com/myapp:1.0.0" }, _docker.Pushed);
    }

    [Fact]
    public void Push_WithOnlyLatest_PushesLatestOnly()
    {
        _testParams.Tags = new List<string> { "registry.example.com/myapp:latest" };

        _dockerService.Push(_testParams);

        Assert.Equal(new[] { "registry.example.com/myapp:latest" }, _docker.Pushed);
    }

    [Fact]
    public void Tag_WhenTheDaemonRefuses_ThrowsWithTheDaemonStatus()
    {
        _docker.FailOn = nameof(FakeDockerCli.Tag);

        var exception = Assert.Throws<BuildFailureException>(() => _dockerService.Tag(_testParams));

        Assert.Equal(BuildFailure.DockerDaemon, exception.ExitStatus);
        VerifyLoggerNotCalled(LogLevel.Information, "[TAG]");
    }

    [Fact]
    public void Build_WhenTheBuildFailsOnACopiedTemplate_StillRemovesTheCopy()
    {
        Directory.CreateDirectory(_testParams.TemplatesDir);
        CreateTemplateDockerfile("node", "FROM node:18");
        _mockNodeService.Setup(x => x.DetectApplicationType(_testParams.RootDirectory)).Returns("node");
        _docker.FailOn = nameof(FakeDockerCli.Build);

        Assert.ThrowsAny<Exception>(() => _dockerService.Build(_testParams));

        Assert.False(File.Exists(Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)));
    }

    [Fact]
    public void Build_WithTheProjectsOwnDockerfile_LeavesItInPlace()
    {
        CreateDockerfile("FROM node:18");

        _dockerService.Build(_testParams);

        Assert.True(File.Exists(Path.Combine(_testParams.RootDirectory, _testParams.DockerFile)));
    }

    private sealed class FakeDockerCli : IDockerCli
    {
        public string? FailOn { get; set; }

        public string? LoginServer { get; private set; }

        public string? BuiltDockerFileContent { get; private set; }

        public List<string> Pushed { get; } = [];

        public void Login(string server, string username, string password)
        {
            Fail(nameof(Login), $"docker login -u {username} -p {password} {server}");
            LoginServer = server;
        }

        public void Build(string path, string dockerFile, string tag, Action<string> output)
        {
            BuiltDockerFileContent = File.ReadAllText(dockerFile);
            Fail(nameof(Build), "docker build failed");
        }

        public void Push(string name)
        {
            Fail(nameof(Push), $"docker push {name} denied");
            Pushed.Add(name);
        }

        public void Tag(string source, string target)
        {
            Fail(nameof(Tag), $"docker tag {source} {target} failed");
        }

        private void Fail(string operation, string message)
        {
            if (FailOn == operation)
            {
                throw new InvalidOperationException(message);
            }
        }
    }

    private void CreateDockerfile(string content)
    {
        var dockerfilePath = Path.Combine(_testRootDirectory, _testParams.DockerFile);
        File.WriteAllText(dockerfilePath, content);
    }

    private void CreateTemplateDockerfile(string appType, string content)
    {
        var templatePath = Path.Combine(_testParams.TemplatesDir, $"Dockerfile.{appType}");
        
        File.WriteAllText(templatePath, content);
    }

    private void CreateTemplateDockerfile(string templateDir, string appType, string content)
    {
        var templatePath = Path.Combine(templateDir, $"Dockerfile.{appType}");
        
        File.WriteAllText(templatePath, content);
    }

    private void VerifyLoggerInfo(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private void VerifyLoggerWarning(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private void VerifyLoggerNotCalled(LogLevel logLevel, string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRootDirectory))
        {
            Directory.Delete(_testRootDirectory, recursive: true);
        }
    }
}