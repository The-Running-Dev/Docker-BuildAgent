extern alias NodeAssembly;

using Xunit;

using Entities;
using Parameters;

namespace Node.Tests;

public sealed class NodeBuildTests : IDisposable
{
    private readonly string _rootDir;

    private readonly string _artifactsDir;

    public NodeBuildTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-node-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);

        _artifactsDir = Path.Combine(_rootDir, "artifacts");
        Directory.CreateDirectory(_artifactsDir);
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
        var build = new NodeBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.NotNull(build.Parameters);
        Assert.NotNull(build.Parameters.ArtifactsDir);
    }

    [Fact]
    public void Parameters_SetsArtifactsDirCorrectly()
    {
        var build = new NodeBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.Equal(_artifactsDir, build.Parameters.ArtifactsDir);
    }

    [Fact]
    public void Parameters_SetsVersionCorrectly()
    {
        var build = new NodeBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.NotNull(build.Parameters.Version);
        Assert.Equal("2.0.1", build.Parameters.Version.Version);
    }

    private NodeParams CreateParameters()
    {
        return new NodeParams
        {
            RootDirectory = _rootDir,
            ArtifactsDir = _artifactsDir,
            Version = new VersionInfo { Version = "2.0.1" }
        };
    }

    private sealed class NodeBuild : NodeAssembly::Node
    {
        public void SetParameters(NodeParams parameters) => Parameters = parameters;
    }
}
