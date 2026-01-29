extern alias NodeInDockerAssembly;

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
    public void Parameters_InitializesWithDefaults()
    {
        var build = new NodeInDockerBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.NotNull(build.Parameters);
        Assert.NotNull(build.Parameters.ArtifactsDir);
        Assert.NotNull(build.Parameters.TemplatesDir);
    }

    [Fact]
    public void Parameters_SetsBothNodeAndDockerDirs()
    {
        var build = new NodeInDockerBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.Equal(_artifactsDir, build.Parameters.ArtifactsDir);
        Assert.Equal(_templatesDir, build.Parameters.TemplatesDir);
    }

    [Fact]
    public void Parameters_SetsVersionAndImageTag()
    {
        var build = new NodeInDockerBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.NotNull(build.Parameters.Version);
        Assert.Equal("3.0.0", build.Parameters.Version.Version);
        Assert.NotNull(build.Parameters.ImageTag);
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
        public void SetParameters(NodeInDockerParams parameters) => Parameters = parameters;
    }
}
