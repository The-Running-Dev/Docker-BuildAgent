extern alias NodeAssembly;

using System.Reflection;
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
    public void Configure_UsesTheArtifactsDirField_WhenTheDirectoryExists()
    {
        var elsewhere = Path.Combine(_rootDir, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        var build = new NodeBuild();
        build.SetParameters(CreateParameters("artifacts"));
        SetField(build, "ArtifactsDir", elsewhere);

        build.ConfigureForTest();

        Assert.Equal(elsewhere, build.Parameters.ArtifactsDir);
    }

    [Fact]
    public void Configure_ResolvesARelativeArtifactsDirField_UnderTheRoot()
    {
        var build = new NodeBuild();
        build.SetParameters(CreateParameters("artifacts"));
        SetField(build, "ArtifactsDir", "out");

        build.ConfigureForTest();

        Assert.Equal(Path.Combine(_rootDir, "out"), build.Parameters.ArtifactsDir);
    }

    [Fact]
    public void Configure_KeepsTheParameterValue_WhenTheFieldIsUnset()
    {
        var build = new NodeBuild();
        build.SetParameters(CreateParameters("artifacts"));

        build.ConfigureForTest();

        Assert.Equal(_artifactsDir, build.Parameters.ArtifactsDir);
    }

    private NodeParams CreateParameters(string artifactsDir)
    {
        return new NodeParams
        {
            RootDirectory = _rootDir,
            ArtifactsDir = artifactsDir,
            Version = new VersionInfo { Version = "2.0.1" }
        };
    }

    private static void SetField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found.");

        field.SetValue(target, value);
    }

    private sealed class NodeBuild : NodeAssembly::Node
    {
        public void ConfigureForTest() => Configure();

        public void SetParameters(NodeParams parameters) => Parameters = parameters;
    }
}
