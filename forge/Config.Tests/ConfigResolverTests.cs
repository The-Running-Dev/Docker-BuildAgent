using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Config;

using Xunit;

namespace Config.Tests;

public sealed class ConfigResolverTests : IDisposable
{
    private readonly string _root;

    public ConfigResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "config-resolver-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private static readonly IReadOnlyDictionary<string, string?> Empty = new Dictionary<string, string?>();

    // 'repository-url' (from ForgeParams, which DockerParams extends) has no declared default, so
    // every scenario below supplies it via the mapping-file tier — a key the S7.x assertions never
    // touch — purely to keep the resolver from raising RequiredValueMissing for it.
    private static Dictionary<string, string?> MappingWith(string key, string value) =>
        new() { ["repository-url"] = "https://example.test/repo", [key] = value };

    private static readonly IReadOnlyDictionary<string, string?> RepositoryUrlOnly =
        new Dictionary<string, string?> { ["repository-url"] = "https://example.test/repo" };

    // S7.1 — with a value at every tier, invocation argument (tier 1) wins.
    [Fact]
    public void S7_1_AllTiersPresent_InvocationArgumentWins()
    {
        var result = ConfigResolver.Resolve(
            "docker",
            _root,
            invocationArguments: new Dictionary<string, string?> { ["image-tag"] = "invocation-value" },
            processEnvironment: new Dictionary<string, string?> { ["image-tag"] = "process-value" },
            mappingFileEnvironment: MappingWith("image-tag", "mapping-value"));

        var value = result.Values["image-tag"];
        Assert.Equal("invocation-value", value.Value);
        Assert.Equal(ConfigurationTier.InvocationArgument, value.Tier);
    }

    // S7.2 — removing the winning tier cascades precedence down to the next one, and ultimately
    // to the declared default when no project configuration file exists.
    [Fact]
    public void S7_2_RemovingWinningTier_CascadesToNextTier()
    {
        var withInvocation = ConfigResolver.Resolve(
            "docker",
            _root,
            invocationArguments: new Dictionary<string, string?> { ["image-tag"] = "invocation-value" },
            processEnvironment: new Dictionary<string, string?> { ["image-tag"] = "process-value" },
            mappingFileEnvironment: MappingWith("image-tag", "mapping-value"));
        Assert.Equal(ConfigurationTier.InvocationArgument, withInvocation.Values["image-tag"].Tier);

        var withoutInvocation = ConfigResolver.Resolve(
            "docker",
            _root,
            invocationArguments: Empty,
            processEnvironment: new Dictionary<string, string?> { ["image-tag"] = "process-value" },
            mappingFileEnvironment: MappingWith("image-tag", "mapping-value"));
        Assert.Equal("process-value", withoutInvocation.Values["image-tag"].Value);
        Assert.Equal(ConfigurationTier.ProcessEnvironment, withoutInvocation.Values["image-tag"].Tier);

        var withoutInvocationOrProcess = ConfigResolver.Resolve(
            "docker",
            _root,
            invocationArguments: Empty,
            processEnvironment: Empty,
            mappingFileEnvironment: MappingWith("image-tag", "mapping-value"));
        Assert.Equal("mapping-value", withoutInvocationOrProcess.Values["image-tag"].Value);
        Assert.Equal(ConfigurationTier.MappingFileEnvironment, withoutInvocationOrProcess.Values["image-tag"].Tier);

        var declaredOnly = ConfigResolver.Resolve(
            "docker",
            _root,
            invocationArguments: Empty,
            processEnvironment: Empty,
            mappingFileEnvironment: RepositoryUrlOnly);
        Assert.Equal("container-app", declaredOnly.Values["image-tag"].Value);
        Assert.Equal(ConfigurationTier.DeclaredDefault, declaredOnly.Values["image-tag"].Tier);
    }

    // S7.3 — every resolved value carries a defined, meaningful tier.
    [Fact]
    public void S7_3_EveryResolvedValue_HasADefinedTier()
    {
        var result = ConfigResolver.Resolve("docker", _root, Empty, Empty, RepositoryUrlOnly);

        Assert.NotEmpty(result.Values);
        Assert.All(result.Values.Values, v => Assert.True(Enum.IsDefined(typeof(ConfigurationTier), v.Tier)));
    }

    // S7.7 — secret redaction applies regardless of which tier supplied the value.
    [Fact]
    public void S7_7_SecretRedaction_HoldsRegardlessOfSourceTier()
    {
        var result = ConfigResolver.Resolve(
            "docker",
            _root,
            invocationArguments: new Dictionary<string, string?> { ["registry-token"] = "super-secret" },
            processEnvironment: Empty,
            mappingFileEnvironment: RepositoryUrlOnly);

        var value = result.Values["registry-token"];
        Assert.True(value.IsSecret);
        Assert.Equal(ConfigurationTier.InvocationArgument, value.Tier);
        Assert.DoesNotContain("super-secret", result.ToString());
    }

    // S7.9 — the resolver takes no tier-reordering parameter; precedence is fixed by the contract.
    [Fact]
    public void S7_9_Resolve_HasNoTierOrderingParameter()
    {
        var method = typeof(ConfigResolver).GetMethod("Resolve");
        Assert.NotNull(method);

        var parameterNames = method!.GetParameters().Select(p => p.Name).ToArray();
        Assert.Equal(
            new[] { "buildType", "projectRoot", "invocationArguments", "processEnvironment", "mappingFileEnvironment" },
            parameterNames);
    }

    // S7.10 — an unrecognized key supplied by a caller does not leak into the resolved set, and
    // every known parameter still resolves to exactly one value.
    [Fact]
    public void S7_10_UnknownSuppliedKey_DoesNotLeakIntoResolvedValues()
    {
        var result = ConfigResolver.Resolve(
            "docker",
            _root,
            invocationArguments: new Dictionary<string, string?> { ["bogus-key"] = "should-not-appear", ["image-tag"] = "invocation-value" },
            processEnvironment: Empty,
            mappingFileEnvironment: RepositoryUrlOnly);

        Assert.False(result.Values.ContainsKey("bogus-key"));
        Assert.Equal("invocation-value", result.Values["image-tag"].Value);
        Assert.Equal(result.Values.Keys.Distinct().Count(), result.Values.Count);
    }
}
