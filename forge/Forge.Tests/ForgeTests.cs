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
    public void Configure_AllSource_ChangeLogCoversAllHistory()
    {
        var build = new ForgeBuild();
        build.SetParameters(CreateParameters());
        SetField(build, "ChangeLogSource", "all");

        build.ConfigureForTest();

        Assert.Equal(ChangeLogSource.All, build.Parameters.ChangeLogConfig.Source);
    }

    [Fact]
    public void Configure_TagSource_ChangeLogStartsAtThatTag()
    {
        var build = new ForgeBuild();
        build.SetParameters(CreateParameters());
        SetField(build, "ChangeLogSource", " v1.0.0 ");

        build.ConfigureForTest();

        Assert.Equal(ChangeLogSource.SpecificTag, build.Parameters.ChangeLogConfig.Source);
        Assert.Equal("v1.0.0", build.Parameters.ChangeLogConfig.Tag);
    }

    [Fact]
    public void Cleanup_DeletesTheGeneratedEnvironmentFiles()
    {
        var parameters = CreateParameters();
        Directory.CreateDirectory(parameters.Config!.DirectoryPath);
        File.WriteAllText(parameters.Config.EnvFilePath, "TOKEN=secret");
        File.WriteAllText(parameters.Config.AppEnvFilePath, "API=secret");
        var mapFile = parameters.Config.EnvMapFilePath;
        File.WriteAllText(mapFile, "TOKEN=TOKEN");

        var build = new ForgeBuild();
        build.SetParameters(parameters);

        build.CleanupForTest();

        Assert.False(File.Exists(parameters.Config.EnvFilePath));
        Assert.False(File.Exists(parameters.Config.AppEnvFilePath));
        Assert.True(File.Exists(mapFile));
    }

    [Fact]
    public void Cleanup_WhenNothingWasGenerated_DoesNotThrow()
    {
        var build = new ForgeBuild();
        build.SetParameters(CreateParameters());

        var exception = Record.Exception(() => build.CleanupForTest());

        Assert.Null(exception);
    }

    [Fact]
    public void Cleanup_WithoutAConfig_DoesNotThrow()
    {
        var parameters = CreateParameters();
        parameters.Config = null;
        var build = new ForgeBuild();
        build.SetParameters(parameters);

        var exception = Record.Exception(() => build.CleanupForTest());

        Assert.Null(exception);
    }

    private ForgeParams CreateParameters()
    {
        return new ForgeParams
        {
            Config = new BuildConfig(Nuke.Common.IO.AbsolutePath.Create(_rootDir)),
            RootDirectory = _rootDir,
            Version = new VersionInfo { Version = "1.2.3" },
            ChangeLogConfig = new ChangeLogConfig(ChangeLogSource.LastTag)
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

        public void ConfigureForTest()
        {
            InitializeDependencyInjection();
            Configure();
        }

        public void CleanupForTest() => Cleanup();
    }
}
