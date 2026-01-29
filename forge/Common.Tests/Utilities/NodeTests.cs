using Xunit;

using Utilities;

namespace Common.Tests.Utilities;

public class NodeTests : IDisposable
{
    private readonly string _rootDir;

    public NodeTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-node-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void DetectApplicationType_ReturnsUnknown_WhenPackageJsonMissing()
    {
        var result = Node.DetectApplicationType(_rootDir);

        Assert.Equal("unknown", result);
    }

    [Fact]
    public void DetectApplicationType_ReturnsUnknown_WhenPackageJsonInvalid()
    {
        File.WriteAllText(Path.Combine(_rootDir, "package.json"), "{ invalid json");

        var result = Node.DetectApplicationType(_rootDir);

        Assert.Equal("unknown", result);
    }

    [Fact]
    public void DetectApplicationType_ReturnsAngular_WhenAngularConfigPresent()
    {
        File.WriteAllText(Path.Combine(_rootDir, "package.json"), "{\"dependencies\":{}}" );
        File.WriteAllText(Path.Combine(_rootDir, "angular.json"), "{}");

        var result = Node.DetectApplicationType(_rootDir);

        Assert.Equal("angular", result);
    }
}
