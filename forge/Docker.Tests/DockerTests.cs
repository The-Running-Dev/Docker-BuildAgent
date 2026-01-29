extern alias DockerAssembly;

using System.Reflection;
using Xunit;

using Entities;
using Parameters;

namespace Docker.Tests;

public sealed class DockerTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _templatesDir;

    public DockerTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-docker-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);

        _templatesDir = Path.Combine(_rootDir, "templates");
        Directory.CreateDirectory(_templatesDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void Configure_UsesExplicitTemplatesDir_WhenDirectoryExists()
    {
        var build = new DockerTestBuild();
        build.SetParameters(CreateParameters());
        SetField(build, "TemplatesDir", _templatesDir);

        build.ConfigureForTest();

        Assert.Equal(_templatesDir, build.Parameters.TemplatesDir);
    }

    [Fact]
    public void Configure_UsesRootTemplatesDir_WhenExplicitMissing()
    {
        var build = new DockerTestBuild();
        build.SetParameters(CreateParameters());

        build.ConfigureForTest();

        Assert.Equal(Path.Combine(_rootDir, "templates"), build.Parameters.TemplatesDir);
    }

    [Fact]
    public void Configure_SetsTagsAndReleaseFlags_FromParametersAndFields()
    {
        var build = new DockerTestBuild();
        build.SetParameters(CreateParameters());
        SetField(build, "RegistryUrl", "ghcr.io/acme");
        SetField(build, "ImageTag", "my-app");
        SetField(build, "CreateGitHubRelease", true);
        SetField(build, "PreRelease", true);

        build.ConfigureForTest();

        Assert.Contains("ghcr.io/acme/my-app:latest", build.Parameters.Tags);
        Assert.Contains("ghcr.io/acme/my-app:1.2.3", build.Parameters.Tags);
        Assert.Equal("v1.2.3", build.Parameters.ReleaseTag);
        Assert.True(build.Parameters.CreateGitHubRelease);
        Assert.True(build.Parameters.PreRelease);
    }

    private DockerParams CreateParameters()
    {
        return new DockerParams
        {
            RootDirectory = _rootDir,
            Version = new VersionInfo { Version = "1.2.3" },
            TemplatesDir = "templates"
        };
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        if (field == null)
        {
            throw new InvalidOperationException($"Field '{fieldName}' not found.");
        }

        field.SetValue(target, value);
    }

    private sealed class DockerTestBuild : DockerAssembly::Docker
    {
        public void ConfigureForTest() => Configure();

        public void SetParameters(DockerParams parameters) => Parameters = parameters;
    }
}
