using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using Config;

using Parameters;

using Xunit;
using Xunit.Abstractions;

using YamlDotNet.RepresentationModel;

namespace Config.Tests;

/// <summary>
/// design/30-slices.md § S8 — the ten sample project files under `samples/` are validated unedited
/// by the same loader a real build uses, so a parameter change that invalidates one fails the build
/// (S8.7).
/// </summary>
public sealed class SampleConfigurationTests : IDisposable
{
    private static readonly string[] BuildTypes = { "docker", "node", "node-in-docker", "node-template", "forge" };
    private static readonly string[] Formats = { "yml", "json" };
    private static readonly long[] SupportedSchemaVersions = { 1 };

    private static readonly Dictionary<string, Type?> ParamsTypes = new()
    {
        ["docker"] = typeof(DockerParams),
        ["node"] = typeof(NodeParams),
        ["node-in-docker"] = typeof(NodeInDockerParams),
        ["node-template"] = null,
        ["forge"] = typeof(ForgeParams),
    };

    private static readonly Regex CredentialShape = new(
        @"password|passwd|token|secret|credential|api[-_]?key|bearer",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ITestOutputHelper _output;
    private readonly string _scratch;

    public SampleConfigurationTests(ITestOutputHelper output)
    {
        _output = output;
        _scratch = Path.Combine(Path.GetTempPath(), "sample-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        Directory.Delete(_scratch, recursive: true);
    }

    public static IEnumerable<object[]> AllSamples() =>
        BuildTypes.SelectMany(type => Formats.Select(format => new object[] { type, format }));

    private static string SamplePath(string buildType, string format) =>
        Path.Combine(FindRepositoryRoot(), "samples", buildType, "buildagent." + format);

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "samples")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (a directory holding samples/).");
    }

    /// <summary>Copies a sample, byte for byte, into an empty project root so discovery finds it.</summary>
    private ResolvedConfiguration? LoadSample(string buildType, string format)
    {
        var root = Path.Combine(_scratch, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.Copy(SamplePath(buildType, format), Path.Combine(root, "buildagent." + format));
        try
        {
            return ProjectConfigurationLoader.Load(root);
        }
        catch (ConfigException ex)
        {
            throw new InvalidOperationException($"samples/{buildType}/buildagent.{format}: {ex.Message}", ex);
        }
    }

    // S8.1 — ten samples: one YAML and one JSON for each of the five build types, and nothing else.
    [Fact]
    public void S8_1_TenSamplesExist_OneYamlAndOneJsonPerBuildType()
    {
        var samplesRoot = Path.Combine(FindRepositoryRoot(), "samples");

        var found = Directory.GetFiles(samplesRoot, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(samplesRoot, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var expected = BuildTypes
            .SelectMany(type => Formats.Select(format => $"{type}/buildagent.{format}"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(10, expected.Count);
        Assert.Equal(expected, found);
    }

    // S8.2 — each sample passes validation unedited; the test reports the count validated.
    [Fact]
    public void S8_2_EverySamplePassesValidationUnedited_AndCountIsReported()
    {
        var validated = 0;

        foreach (var args in AllSamples())
        {
            var buildType = (string)args[0];
            var format = (string)args[1];

            var resolved = LoadSample(buildType, format);

            Assert.True(resolved is not null, $"samples/{buildType}/buildagent.{format} was not discovered.");
            validated++;
        }

        _output.WriteLine($"Validated {validated} sample configuration files.");
        Assert.Equal(10, validated);
    }

    // S8.3 — an integer schemaVersion in the supported set, and a buildType from the five.
    [Theory]
    [MemberData(nameof(AllSamples))]
    public void S8_3_SampleDeclaresIntegerSupportedSchemaVersion_AndKnownBuildType(string buildType, string format)
    {
        var (schemaVersion, declaredType, _) = ReadRaw(buildType, format);

        Assert.Contains(schemaVersion, SupportedSchemaVersions);
        Assert.Equal(buildType, declaredType);
        Assert.Contains(declaredType, BuildTypes);
    }

    // S8.4 — every key is in the accepted kebab-case spelling of a real parameter.
    [Theory]
    [MemberData(nameof(AllSamples))]
    public void S8_4_EveryKeyIsAcceptedKebabCaseSpelling(string buildType, string format)
    {
        var (_, _, keys) = ReadRaw(buildType, format);
        var accepted = AcceptedKeys(buildType);

        foreach (var key in keys)
        {
            Assert.Matches("^[a-z][a-z0-9]*(-[a-z0-9]+)*$", key);
            Assert.Contains(key, accepted);
        }
    }

    // S8.5 — no secret-declared parameter and no placeholder credential.
    [Theory]
    [MemberData(nameof(AllSamples))]
    public void S8_5_NoSecretParameterOrPlaceholderCredential(string buildType, string format)
    {
        var (_, _, keys) = ReadRaw(buildType, format);
        var secretKeys = SecretKeys(buildType);

        Assert.Empty(keys.Intersect(secretKeys));

        var text = File.ReadAllText(SamplePath(buildType, format));
        var match = CredentialShape.Match(text);
        Assert.False(match.Success, $"samples/{buildType}/buildagent.{format} contains '{match.Value}'.");
    }

    // S8.6 — the YAML and the JSON sample for a build type express the same settings.
    [Theory]
    [InlineData("docker")]
    [InlineData("node")]
    [InlineData("node-in-docker")]
    [InlineData("node-template")]
    [InlineData("forge")]
    public void S8_6_YamlAndJsonSampleExpressTheSameSettings(string buildType)
    {
        var yaml = LoadSample(buildType, "yml");
        var json = LoadSample(buildType, "json");

        Assert.NotNull(yaml);
        Assert.NotNull(json);
        Assert.Equal(yaml!.Values.Count, json!.Values.Count);

        foreach (var (key, yamlValue) in yaml.Values)
        {
            Assert.True(json.Values.TryGetValue(key, out var jsonValue), $"'{key}' is in the YAML sample only.");
            Assert.Equal(yamlValue, jsonValue);
        }

        // Explicit settings only: the keys the two files themselves declare must match too, since a
        // declared default resolving to the same value would otherwise mask a missing key.
        Assert.Equal(
            ReadRaw(buildType, "yml").Keys.OrderBy(k => k, StringComparer.Ordinal),
            ReadRaw(buildType, "json").Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    // S8.7 — a sample that stops validating fails the build. Proven by feeding the same check a
    // sample corrupted by a parameter that no longer exists, and observing it reject.
    [Fact]
    public void S8_7_SampleWithARemovedParameter_FailsValidation()
    {
        var root = Path.Combine(_scratch, "stale");
        Directory.CreateDirectory(root);
        var text = File.ReadAllText(SamplePath("node", "yml")).Replace("artifacts-dir:", "artifact-directory:");
        File.WriteAllText(Path.Combine(root, "buildagent.yml"), text);

        var ex = Assert.Throws<ConfigException>(() => ProjectConfigurationLoader.Load(root));

        Assert.Contains(ex.Errors, e => e.Code == ConfigErrorCode.UnknownKey && e.Key == "artifact-directory");
    }

    private static HashSet<string> AcceptedKeys(string buildType) =>
        PublicProperties(buildType).Select(p => KebabName(p.Name)).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> SecretKeys(string buildType) =>
        PublicProperties(buildType)
            .Where(p => p.GetCustomAttribute<SecretParameterAttribute>() is not null)
            .Select(p => KebabName(p.Name))
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<PropertyInfo> PublicProperties(string buildType) =>
        ParamsTypes[buildType]?.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        ?? Enumerable.Empty<PropertyInfo>();

    private static string KebabName(string propertyName) =>
        Regex.Replace(propertyName, "([a-z])([A-Z])", "$1-$2").ToLowerInvariant();

    /// <summary>
    /// Reads a sample without the loader, so the raw shape (an integer, not a quoted string) is what
    /// is checked rather than whatever the loader tolerates.
    /// </summary>
    private static (long SchemaVersion, string BuildType, List<string> Keys) ReadRaw(string buildType, string format)
    {
        var text = File.ReadAllText(SamplePath(buildType, format));
        return format == "json" ? ReadJson(text) : ReadYaml(text);
    }

    private static (long, string, List<string>) ReadJson(string text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var version = root.GetProperty("schemaVersion");
        Assert.Equal(JsonValueKind.Number, version.ValueKind);

        var keys = root.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateObject().Select(p => p.Name).ToList()
            : new List<string>();

        return (version.GetInt64(), root.GetProperty("buildType").GetString()!, keys);
    }

    private static (long, string, List<string>) ReadYaml(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        var version = (YamlScalarNode)root.Children[new YamlScalarNode("schemaVersion")];
        Assert.Equal(YamlDotNet.Core.ScalarStyle.Plain, version.Style);
        Assert.True(long.TryParse(version.Value, out var schemaVersion), "schemaVersion is not an integer.");

        var buildType = ((YamlScalarNode)root.Children[new YamlScalarNode("buildType")]).Value!;

        var keys = root.Children.TryGetValue(new YamlScalarNode("parameters"), out var parameters)
            ? ((YamlMappingNode)parameters).Children.Keys.Select(k => ((YamlScalarNode)k).Value!).ToList()
            : new List<string>();

        return (schemaVersion, buildType, keys);
    }
}
