using Xunit;

using Utilities;

namespace Common.Tests.Utilities;

public class EnvironmentLoaderTests : IDisposable
{
    private readonly string _rootDir;
    private const string ExistingVar = "BUILD_AGENT_ENV_LOADER_EXISTING";
    private const string NewVar = "BUILD_AGENT_ENV_LOADER_NEW";

    public EnvironmentLoaderTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-env-loader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ExistingVar, null);
        Environment.SetEnvironmentVariable(NewVar, null);

        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    // I22 — a value already present in the process environment is not overwritten by a
    // conflicting value loaded from a .env file.
    [Fact]
    public void LoadWithoutClobbering_DoesNotOverwrite_ExistingProcessVariable()
    {
        Environment.SetEnvironmentVariable(ExistingVar, "process-value");
        var path = Path.Combine(_rootDir, ".env");
        File.WriteAllLines(path, new[] { $"{ExistingVar}=file-value" });

        EnvironmentLoader.LoadWithoutClobbering(path);

        Assert.Equal("process-value", Environment.GetEnvironmentVariable(ExistingVar));
    }

    [Fact]
    public void LoadWithoutClobbering_Loads_PreviouslyUnsetVariable()
    {
        var path = Path.Combine(_rootDir, ".env");
        File.WriteAllLines(path, new[] { $"{NewVar}=file-value" });

        EnvironmentLoader.LoadWithoutClobbering(path);

        Assert.Equal("file-value", Environment.GetEnvironmentVariable(NewVar));
    }
}
