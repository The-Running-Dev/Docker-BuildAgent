extern alias ForgeAssembly;

using System.Reflection;
using Xunit;

using Entities;
using Parameters;

namespace Forge.Tests;

public sealed class ForgeBuildTests : IDisposable
{
    private readonly string _rootDir;

    public ForgeBuildTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "build-agent-forge-tests", Guid.NewGuid().ToString("N"));
        
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
    public void Parameters_InitializesWithDefaults()
    {
        var build = new ForgeBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.NotNull(build.Parameters);
        Assert.NotNull(build.Parameters.ChangeLogConfig);
        Assert.Equal(ChangeLogSource.All, build.Parameters.ChangeLogConfig.Source);
    }

    [Fact]
    public void Parameters_CanBeSetDirectly()
    {
        var build = new ForgeBuild();
        var parameters = CreateParameters();
        parameters.ChangeLogConfig = new ChangeLogConfig(ChangeLogSource.All);
        build.SetParameters(parameters);

        Assert.Equal(ChangeLogSource.All, build.Parameters.ChangeLogConfig.Source);
    }

    [Fact]
    public void Parameters_SetsVersionCorrectly()
    {
        var build = new ForgeBuild();
        var parameters = CreateParameters();
        build.SetParameters(parameters);

        Assert.NotNull(build.Parameters.Version);
        Assert.Equal("1.2.3", build.Parameters.Version.Version);
    }

    private ForgeParams CreateParameters()
    {
        return new ForgeParams
        {
            RootDirectory = _rootDir,
            Version = new VersionInfo { Version = "1.2.3" },
            ChangeLogConfig = new ChangeLogConfig(ChangeLogSource.All)
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

    private sealed class ForgeBuild : ForgeAssembly::Forge
    {
        public void SetParameters(ForgeParams parameters) => Parameters = parameters;
    }
}
