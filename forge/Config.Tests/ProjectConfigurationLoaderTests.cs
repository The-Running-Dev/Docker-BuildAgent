using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Config;

using Xunit;

namespace Config.Tests;

public sealed class ProjectConfigurationLoaderTests : IDisposable
{
    private readonly string _root;

    public ProjectConfigurationLoaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "config-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private string WriteFile(string name, string contents)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, contents);
        return path;
    }

    private static ConfigException AssertFails(Action act)
    {
        return Assert.Throws<ConfigException>(() => act());
    }

    // S6.1 — discovery finds only buildagent.yml/.yaml/.json at the project root.
    [Fact]
    public void S6_1_NoConfigurationFile_ReturnsNull()
    {
        WriteFile("notes.txt", "not a config file");

        var result = ProjectConfigurationLoader.Load(_root);

        Assert.Null(result);
    }

    [Fact]
    public void S6_1_FileInSubdirectory_IsNotDiscovered()
    {
        var subdir = Path.Combine(_root, "sub");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Combine(subdir, "buildagent.yml"), "schemaVersion: 1\nbuildType: forge\n");

        var result = ProjectConfigurationLoader.Load(_root);

        Assert.Null(result);
    }

    [Fact]
    public void S6_1_BuildagentYml_IsDiscoveredAndResolved()
    {
        WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: forge
            parameters:
              repository-url: "https://example.com/repo.git"
            """);

        var result = ProjectConfigurationLoader.Load(_root);

        Assert.NotNull(result);
    }

    // S6.1/S6.4 — a well-formed JSON file loads; its integer schemaVersion is an integer, not a double.
    [Fact]
    public void S6_4_JsonIntegerSchemaVersion_IsAccepted()
    {
        WriteFile("buildagent.json", """
            { "schemaVersion": 1, "buildType": "forge", "parameters": { "repository-url": "https://example.com/repo.git" } }
            """);

        var result = ProjectConfigurationLoader.Load(_root);

        Assert.NotNull(result);
    }

    // S6.2 — two matching files fail with MultipleConfigurationFiles naming every path.
    [Fact]
    public void S6_2_YmlAndYaml_BothPresent_FailsWithMultipleConfigurationFiles()
    {
        var ymlPath = WriteFile("buildagent.yml", "schemaVersion: 1\nbuildType: forge\n");
        var yamlPath = WriteFile("buildagent.yaml", "schemaVersion: 1\nbuildType: forge\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors);
        Assert.Equal(ConfigErrorCode.MultipleConfigurationFiles, error.Code);
        Assert.Contains(ymlPath, error.Message);
        Assert.Contains(yamlPath, error.Message);
    }

    // S6.3 — a missing schemaVersion fails with SchemaVersionMissing, naming the supported versions.
    [Fact]
    public void S6_3_MissingSchemaVersion_FailsWithSchemaVersionMissing()
    {
        WriteFile("buildagent.yml", "buildType: forge\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.SchemaVersionMissing);
        Assert.Contains("1", error.Message);
    }

    // S6.4 — an unsupported or non-integer schemaVersion fails with SchemaVersionUnsupported.
    [Fact]
    public void S6_4_UnsupportedSchemaVersion_FailsWithSchemaVersionUnsupported()
    {
        WriteFile("buildagent.yml", "schemaVersion: 2\nbuildType: forge\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.SchemaVersionUnsupported);
    }

    [Fact]
    public void S6_4_NonIntegerSchemaVersion_FailsWithSchemaVersionUnsupported()
    {
        WriteFile("buildagent.yml", "schemaVersion: \"one\"\nbuildType: forge\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.SchemaVersionUnsupported);
    }

    // S6.5 — an unknown parameter key fails with UnknownKey, naming the key and the nearest known key.
    [Fact]
    public void S6_5_UnknownKey_FailsNamingKeyAndNearestKnownKey()
    {
        WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: docker
            parameters:
              repository-url: "https://example.com/repo.git"
              image-tagg: "x"
            """);

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.UnknownKey);
        Assert.Equal("image-tagg", error.Key);
        Assert.Contains("image-tag", error.Message);
    }

    // S6.6 — a key is accepted in exactly one spelling: kebab-case. camelCase fails with UnknownKey.
    [Fact]
    public void S6_6_CamelCaseSpelling_FailsWithUnknownKey()
    {
        WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: docker
            parameters:
              repository-url: "https://example.com/repo.git"
              imageTag: "x"
            """);

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.UnknownKey);
        Assert.Equal("imageTag", error.Key);
    }

    // S6.7 — a secret-declared parameter present in the file fails with SecretKeyRejected and never
    // appears in any output stream.
    [Fact]
    public void S6_7_SecretParameter_FailsWithSecretKeyRejected_AndValueNeverAppearsInMessage()
    {
        WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: docker
            parameters:
              repository-url: "https://example.com/repo.git"
              registry-token: "super-secret-value"
            """);

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.SecretKeyRejected);
        Assert.Equal("registry-token", error.Key);
        Assert.DoesNotContain("super-secret-value", error.Message);
        Assert.DoesNotContain("super-secret-value", ex.Message);
    }

    // S6.8 — a value that cannot convert to the declared type fails with ValueTypeMismatch, naming
    // the key, the declared type and the value's shape, never the value itself (I25).
    [Fact]
    public void S6_8_ValueTypeMismatch_NamesKeyTypeAndShape_NeverTheValue()
    {
        WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: docker
            parameters:
              repository-url: "https://example.com/repo.git"
              create-git-hub-release: "not-a-boolean-value"
            """);

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.ValueTypeMismatch);
        Assert.Equal("create-git-hub-release", error.Key);
        Assert.Contains("Boolean", error.Message);
        Assert.Contains("string", error.Message);
        Assert.DoesNotContain("not-a-boolean-value", error.Message);
    }

    // S6.9 — a buildType that is absent or outside the five fails with UnknownBuildType naming the
    // accepted five.
    [Fact]
    public void S6_9_MissingBuildType_FailsWithUnknownBuildType()
    {
        WriteFile("buildagent.yml", "schemaVersion: 1\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.UnknownBuildType);
        Assert.Contains("docker", error.Message);
        Assert.Contains("node-template", error.Message);
    }

    [Fact]
    public void S6_9_UnrecognizedBuildType_FailsWithUnknownBuildType()
    {
        WriteFile("buildagent.yml", "schemaVersion: 1\nbuildType: not-a-real-type\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.UnknownBuildType);
    }

    // S6.10 — a non-nullable parameter with no value at any tier fails with RequiredValueMissing,
    // naming the key and the tiers consulted.
    [Fact]
    public void S6_10_RequiredParameterAbsent_FailsWithRequiredValueMissing()
    {
        WriteFile("buildagent.yml", "schemaVersion: 1\nbuildType: docker\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.RequiredValueMissing);
        Assert.Equal("repository-url", error.Key);
        Assert.Contains("project configuration file", error.Message);
        Assert.Contains("declared default", error.Message);
    }

    // S6.11 — a file that is not well-formed, or uses a construct with no JSON equivalent, fails with
    // MalformedDocument naming the path and position.
    [Fact]
    public void S6_11_MalformedYaml_FailsWithMalformedDocument()
    {
        var path = WriteFile("buildagent.yml", "schemaVersion: 1\n  buildType: docker\nbroken: [1, 2\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors);
        Assert.Equal(ConfigErrorCode.MalformedDocument, error.Code);
        Assert.Contains(path, error.Message);
    }

    [Fact]
    public void S6_11_YamlAnchor_FailsWithMalformedDocument()
    {
        WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: &bt docker
            """);

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.MalformedDocument);
    }

    [Fact]
    public void S6_11_MultipleYamlDocuments_FailsWithMalformedDocument()
    {
        WriteFile("buildagent.yml", "schemaVersion: 1\nbuildType: docker\n---\nschemaVersion: 1\n");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.MalformedDocument);
    }

    [Fact]
    public void S6_11_MalformedJson_FailsWithMalformedDocument()
    {
        var path = WriteFile("buildagent.json", "{ \"schemaVersion\": 1, ");

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        var error = Assert.Single(ex.Errors);
        Assert.Equal(ConfigErrorCode.MalformedDocument, error.Code);
        Assert.Contains(path, error.Message);
    }

    // S6.12 — a file that exists but cannot be opened fails with FileUnreadable.
    [Fact]
    public void S6_12_UnreadableFile_FailsWithFileUnreadable()
    {
        var path = WriteFile("buildagent.yml", "schemaVersion: 1\nbuildType: forge\n");

        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));
            Assert.Single(ex.Errors, e => e.Code == ConfigErrorCode.FileUnreadable);
        }
    }

    // S6.13 — a file containing four distinct errors reports all four in one pass (I18).
    [Fact]
    public void S6_13_FourDistinctErrors_AllReportedInOnePass()
    {
        WriteFile("buildagent.yml", """
            buildType: docker
            parameters:
              repository-url: "https://example.com/repo.git"
              registry-token: "secret"
              totally-unknown-key: "x"
              create-git-hub-release: "not-a-boolean"
            """);

        var ex = AssertFails(() => ProjectConfigurationLoader.Load(_root));

        Assert.Equal(4, ex.Errors.Count);
        var codes = ex.Errors.Select(e => e.Code).ToHashSet();
        Assert.Equal(
            new HashSet<ConfigErrorCode>
            {
                ConfigErrorCode.SchemaVersionMissing,
                ConfigErrorCode.SecretKeyRejected,
                ConfigErrorCode.UnknownKey,
                ConfigErrorCode.ValueTypeMismatch,
            },
            codes);
    }

    // S6.15 — resolution creates and modifies no file: no new file appears alongside the project file.
    [Fact]
    public void S6_15_Resolution_CreatesNoFile()
    {
        WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: forge
            parameters:
              repository-url: "https://example.com/repo.git"
            """);

        var before = Directory.GetFiles(_root).OrderBy(f => f).ToArray();
        ProjectConfigurationLoader.Load(_root);
        var after = Directory.GetFiles(_root).OrderBy(f => f).ToArray();

        Assert.Equal(before, after);
    }

    // S6.16 — the project file's bytes are identical before and after, including a failed validation.
    [Fact]
    public void S6_16_FileBytes_UnchangedAfterSuccessfulResolution()
    {
        var path = WriteFile("buildagent.yml", """
            schemaVersion: 1
            buildType: forge
            parameters:
              repository-url: "https://example.com/repo.git"
            """);
        var before = File.ReadAllBytes(path);

        ProjectConfigurationLoader.Load(_root);

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void S6_16_FileBytes_UnchangedAfterFailedValidation()
    {
        var path = WriteFile("buildagent.yml", "buildType: not-a-real-type\n");
        var before = File.ReadAllBytes(path);

        Assert.Throws<ConfigException>(() => ProjectConfigurationLoader.Load(_root));

        Assert.Equal(before, File.ReadAllBytes(path));
    }
}
