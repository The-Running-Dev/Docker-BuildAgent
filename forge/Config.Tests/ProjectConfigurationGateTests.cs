using System;
using System.Collections.Generic;
using System.IO;

using Xunit;

namespace Config.Tests;

public sealed class ProjectConfigurationGateTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> NoMapping = new Dictionary<string, string>();

    private readonly string _root;
    private readonly List<string> _errors = new();

    public ProjectConfigurationGateTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "config-gate-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteFile(string contents) => File.WriteAllText(Path.Combine(_root, "buildagent.yml"), contents);

    private ConfigurationGateOutcome Resolve(string buildType, params string[] args) =>
        new ProjectConfigurationGate(buildType).Resolve(_root, args, NoMapping, _errors.Add);

    // S13.7 — no file: valid, nothing applied, so effective values are exactly the pre-S13 ones.
    [Theory]
    [InlineData("docker")]
    [InlineData("node")]
    [InlineData("node-in-docker")]
    [InlineData("forge")]
    public void S13_7_NoFile_IsValidAndAppliesNothing(string buildType)
    {
        var outcome = Resolve(buildType);

        Assert.True(outcome.IsValid);
        Assert.Empty(outcome.ProjectFileValues);
        Assert.Empty(_errors);
    }

    // S13.5 — a file-only value is the effective parameter, attributed to the project file.
    [Fact]
    public void S13_5_FileOnlyValue_IsAppliedAndAttributedToTheFile()
    {
        WriteFile("schemaVersion: 1\nbuildType: docker\nparameters:\n  image-tag: from-file\n");

        var outcome = Resolve("docker");

        Assert.True(outcome.IsValid);
        Assert.Equal("from-file", outcome.ProjectFileValues["ImageTag"]);
        Assert.Equal(ConfigurationTier.ProjectConfigurationFile, outcome.Resolved!.Values["image-tag"].Tier);
    }

    // S13.6 — an argument, the process environment or the mapping file still beats the file.
    [Fact]
    public void S13_6_ArgumentBeatsFile()
    {
        WriteFile("schemaVersion: 1\nbuildType: docker\nparameters:\n  image-tag: from-file\n");

        var outcome = Resolve("docker", "--image-tag", "from-arg");

        Assert.DoesNotContain("ImageTag", outcome.ProjectFileValues.Keys);
        Assert.Equal(ConfigurationTier.InvocationArgument, outcome.Resolved!.Values["image-tag"].Tier);
    }

    [Fact]
    public void S13_6_ArgumentEqualsFormBeatsFile()
    {
        WriteFile("schemaVersion: 1\nbuildType: docker\nparameters:\n  image-tag: from-file\n");

        var outcome = Resolve("docker", "--image-tag=from-arg");

        Assert.Equal(ConfigurationTier.InvocationArgument, outcome.Resolved!.Values["image-tag"].Tier);
    }

    [Fact]
    public void S13_6_ProcessEnvironmentBeatsFile()
    {
        WriteFile("schemaVersion: 1\nbuildType: docker\nparameters:\n  image-tag: from-file\n");
        Environment.SetEnvironmentVariable("ImageTag", "from-env");
        try
        {
            var outcome = Resolve("docker");

            Assert.DoesNotContain("ImageTag", outcome.ProjectFileValues.Keys);
            Assert.Equal(ConfigurationTier.ProcessEnvironment, outcome.Resolved!.Values["image-tag"].Tier);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ImageTag", null);
        }
    }

    [Fact]
    public void S13_6_MappingFileBeatsFile()
    {
        WriteFile("schemaVersion: 1\nbuildType: docker\nparameters:\n  image-tag: from-file\n");

        var outcome = new ProjectConfigurationGate("docker").Resolve(
            _root, Array.Empty<string>(), new Dictionary<string, string> { ["ImageTag"] = "from-map" }, _errors.Add);

        Assert.DoesNotContain("ImageTag", outcome.ProjectFileValues.Keys);
        Assert.Equal(ConfigurationTier.MappingFileEnvironment, outcome.Resolved!.Values["image-tag"].Tier);
    }

    // S13.3 — every error is reported in one pass; each names file, key and rule; never a value.
    [Fact]
    public void S13_3_AllErrorsReportedTogether_WithoutValues()
    {
        WriteFile(
            "schemaVersion: 1\nbuildType: docker\nparameters:\n" +
            "  bogus-key: x\n  registry-token: hush-hush\n  create-git-hub-release: nope\n  pre-release: nope\n");

        var outcome = Resolve("docker");

        Assert.False(outcome.IsValid);
        Assert.Equal(4, _errors.Count);
        Assert.All(_errors, e => Assert.Contains("buildagent.yml", e));
        Assert.All(_errors, e => Assert.DoesNotContain("hush-hush", e));
        Assert.Contains(_errors, e => e.Contains("bogus-key") && e.Contains("UnknownKey"));
        Assert.Contains(_errors, e => e.Contains("registry-token") && e.Contains("SecretKeyRejected"));
    }

    // S13.7 — the repository URL is derived by the build itself, never a configuration error.
    [Fact]
    public void S13_7_RepositoryUrlIsNotRequiredFromConfiguration()
    {
        var outcome = Resolve("forge");

        Assert.True(outcome.IsValid);
    }
}
