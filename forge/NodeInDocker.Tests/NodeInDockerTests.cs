extern alias NodeInDockerAssembly;

using System.Reflection;
using Xunit;

using Entities;
using Parameters;

namespace NodeInDocker.Tests;

public sealed class NodeInDockerBuildTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _artifactsDir;
    private readonly string _templatesDir;

    public NodeInDockerBuildTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-nodeindocker-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);

        _artifactsDir = Path.Combine(_rootDir, "artifacts");
        Directory.CreateDirectory(_artifactsDir);

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
    public void Configure_ResolvesRelativeDirectoryFields_UnderTheRoot()
    {
        var build = new NodeInDockerBuild();
        build.SetParameters(CreateParameters());
        SetField(build, "ArtifactsDir", "out");
        SetField(build, "TemplatesDir", "tpl");

        build.ConfigureForTest();

        Assert.Equal(Path.Combine(_rootDir, "out"), build.Parameters.ArtifactsDir);
        Assert.Equal(Path.Combine(_rootDir, "tpl"), build.Parameters.TemplatesDir);
    }

    [Fact]
    public void Configure_FieldsOverrideTheParameterRegistryAndImageTag()
    {
        var parameters = CreateParameters();
        parameters.RegistryUrl = "registry.example.com/old";
        var build = new NodeInDockerBuild();
        build.SetParameters(parameters);
        SetField(build, "RegistryUrl", "ghcr.io/acme");
        SetField(build, "ImageTag", "renamed");

        build.ConfigureForTest();

        Assert.Equal(["ghcr.io/acme/renamed:latest"], build.Parameters.Tags);
    }

    [Fact]
    public void Configure_UnsetFields_KeepTheParameterValues_AndAnEmptyRegistryAddsNoPrefix()
    {
        var build = new NodeInDockerBuild();
        build.SetParameters(CreateParameters());

        build.ConfigureForTest();

        Assert.Equal(_artifactsDir, build.Parameters.ArtifactsDir);
        Assert.Equal(_templatesDir, build.Parameters.TemplatesDir);
        Assert.Equal(["node-docker-app:latest"], build.Parameters.Tags);
    }

    // S3.12/I15: a push to `main` (CreateGitHubRelease false) moves `latest` only and writes no
    // versioned tag; the same rule the Docker build follows.
    [Fact]
    public void Configure_OmitsVersionedTag_WhenCreateGitHubReleaseIsFalse()
    {
        var build = new NodeInDockerBuild();
        build.SetParameters(CreateParameters());
        SetField(build, "RegistryUrl", "ghcr.io/acme");
        SetField(build, "ImageTag", "node-docker-app");
        SetField(build, "CreateGitHubRelease", false);

        build.ConfigureForTest();

        Assert.Equal(["ghcr.io/acme/node-docker-app:latest"], build.Parameters.Tags);
        Assert.False(build.Parameters.CreateGitHubRelease);
    }

    [Fact]
    public void Configure_AddsVersionedTag_WhenCreateGitHubReleaseIsTrue()
    {
        var build = new NodeInDockerBuild();
        build.SetParameters(CreateParameters());
        SetField(build, "RegistryUrl", "ghcr.io/acme");
        SetField(build, "ImageTag", "node-docker-app");
        SetField(build, "CreateGitHubRelease", true);

        build.ConfigureForTest();

        Assert.Equal(["ghcr.io/acme/node-docker-app:latest", "ghcr.io/acme/node-docker-app:3.0.0"], build.Parameters.Tags);
        Assert.Equal("v3.0.0", build.Parameters.ReleaseTag);
        Assert.True(build.Parameters.CreateGitHubRelease);
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");

        field.SetValue(target, value);
    }

    private NodeInDockerParams CreateParameters()
    {
        return new NodeInDockerParams
        {
            RootDirectory = _rootDir,
            ArtifactsDir = _artifactsDir,
            TemplatesDir = _templatesDir,
            Version = new VersionInfo { Version = "3.0.0" },
            ImageTag = "node-docker-app"
        };
    }

    private sealed class NodeInDockerBuild : NodeInDockerAssembly::NodeInDocker
    {
        public void ConfigureForTest() => Configure();

        public void SetParameters(NodeInDockerParams parameters) => Parameters = parameters;
    }
}
