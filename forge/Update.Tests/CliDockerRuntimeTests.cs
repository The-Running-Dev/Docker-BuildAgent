#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Update;
using Xunit;

namespace Update.Tests;

/// <summary>
/// Drives <see cref="CliDockerRuntime"/> through its process-runner seam: the exact `docker` argument sequences
/// it issues and how it reads what comes back, without a daemon. The live-daemon counterpart is
/// <see cref="RoundTripProbeTests"/>.
/// </summary>
public sealed class CliDockerRuntimeTests
{
    private const string ContainerId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ImageId = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private sealed class FakeRunner
    {
        public List<string> Calls { get; } = new();

        public Func<IReadOnlyList<string>, (int, string, string)> Respond { get; set; } = _ => (0, string.Empty, string.Empty);

        public Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(IReadOnlyList<string> args)
        {
            Calls.Add(string.Join(' ', args));
            return Task.FromResult(Respond(args));
        }
    }

    private static (CliDockerRuntime Runtime, FakeRunner Runner, StringWriter Warnings) Create()
    {
        var runner = new FakeRunner();
        var warnings = new StringWriter();
        return (new CliDockerRuntime(runner.RunAsync, warnings), runner, warnings);
    }

    // A container created by `docker run -d --name web <image>` on a current daemon, trimmed to the fields the
    // mapping reads; values are the daemon's own defaults.
    private static JsonObject DefaultInspect() => new()
    {
        ["Id"] = ContainerId,
        ["Name"] = "/web",
        ["Image"] = ImageId,
        ["State"] = new JsonObject { ["Running"] = true },
        ["Config"] = new JsonObject
        {
            ["Hostname"] = ContainerId[..12],
            ["Domainname"] = "",
            ["User"] = "",
            ["Tty"] = false,
            ["OpenStdin"] = false,
            ["StdinOnce"] = false,
            ["Env"] = new JsonArray("PATH=/usr/bin"),
            ["Cmd"] = new JsonArray("serve"),
            ["Image"] = "web:1.0",
            ["WorkingDir"] = "/app",
            ["Entrypoint"] = null,
            ["Labels"] = new JsonObject(),
        },
        ["HostConfig"] = new JsonObject
        {
            ["LogConfig"] = new JsonObject { ["Type"] = "json-file", ["Config"] = new JsonObject() },
            ["NetworkMode"] = "bridge",
            ["PortBindings"] = new JsonObject(),
            ["RestartPolicy"] = new JsonObject { ["Name"] = "no", ["MaximumRetryCount"] = 0 },
            ["AutoRemove"] = false,
            ["CapAdd"] = null,
            ["CapDrop"] = null,
            ["Dns"] = new JsonArray(),
            ["ExtraHosts"] = null,
            ["IpcMode"] = "private",
            ["Cgroup"] = "",
            ["Links"] = null,
            ["OomScoreAdj"] = 0,
            ["PidMode"] = "",
            ["Privileged"] = false,
            ["PublishAllPorts"] = false,
            ["ReadonlyRootfs"] = false,
            ["SecurityOpt"] = null,
            ["UTSMode"] = "",
            ["UsernsMode"] = "",
            ["ShmSize"] = 67108864,
            ["Runtime"] = "runc",
            ["CpuShares"] = 0,
            ["Memory"] = 0,
            ["NanoCpus"] = 0,
            ["CgroupParent"] = "",
            ["Devices"] = new JsonArray(),
            ["MemorySwappiness"] = null,
            ["PidsLimit"] = null,
            ["Ulimits"] = new JsonArray(),
            ["Init"] = null,
        },
        ["Mounts"] = new JsonArray(),
        ["NetworkSettings"] = new JsonObject
        {
            ["Networks"] = new JsonObject
            {
                ["bridge"] = new JsonObject { ["IPAMConfig"] = null, ["Aliases"] = null, ["DriverOpts"] = null },
            },
        },
    };

    private static string ImageConfig(string workingDir = "/app", string user = "") =>
        new JsonObject { ["User"] = user, ["WorkingDir"] = workingDir, ["Cmd"] = new JsonArray("serve") }.ToJsonString();

    private static Func<IReadOnlyList<string>, (int, string, string)> Daemon(JsonObject inspect, string? imageConfig = null) =>
        args => args[0] switch
        {
            "inspect" => (0, new JsonArray(inspect).ToJsonString(), string.Empty),
            "image" when args.Contains("{{json .Config}}") => imageConfig != null
                ? (0, imageConfig, string.Empty)
                : (1, string.Empty, "Error: No such image"),
            _ => (0, string.Empty, string.Empty),
        };

    // Contract § Global tool: the reference is "resolved afresh against the registry" — the pull runs first
    // even when an image under that tag is already local, so a tag that moved upstream is picked up.
    [Fact]
    public async Task ResolveImageIdAsync_PullsBeforeReadingTheLocalId()
    {
        var (runtime, runner, warnings) = Create();
        runner.Respond = args => args[0] == "image" ? (0, ImageId + "\n", string.Empty) : (0, string.Empty, string.Empty);

        var id = await runtime.ResolveImageIdAsync("web:2.0");

        Assert.Equal(ImageId, id);
        Assert.Equal(new[] { "pull web:2.0", "image inspect --format {{.Id}} web:2.0" }, runner.Calls);
        Assert.Empty(warnings.ToString());
    }

    [Fact]
    public async Task ResolveImageIdAsync_PullFails_UsesTheLocalImage_AndWarns()
    {
        var (runtime, runner, warnings) = Create();
        runner.Respond = args => args[0] == "pull"
            ? (1, string.Empty, "dial tcp: lookup registry: no such host")
            : (0, ImageId, string.Empty);

        var id = await runtime.ResolveImageIdAsync("web:2.0");

        Assert.Equal(ImageId, id);
        Assert.Contains("could not be pulled", warnings.ToString());
        Assert.Contains("may be out of date", warnings.ToString());
    }

    [Fact]
    public async Task ResolveImageIdAsync_PullFails_NoLocalImage_Throws()
    {
        var (runtime, runner, _) = Create();
        runner.Respond = args => args[0] == "pull"
            ? (1, string.Empty, "manifest unknown")
            : (1, string.Empty, "No such image");

        var ex = await Assert.ThrowsAsync<DockerRuntimeException>(() => runtime.ResolveImageIdAsync("web:2.0"));
        Assert.Contains("manifest unknown", ex.Message);
    }

    // An image id names exactly one image and exists only locally; there is nothing to pull.
    [Fact]
    public async Task ResolveImageIdAsync_ImageId_IsNeverPulled()
    {
        var (runtime, runner, _) = Create();
        runner.Respond = _ => (0, ImageId, string.Empty);

        await runtime.ResolveImageIdAsync(ImageId);

        Assert.DoesNotContain(runner.Calls, call => call.StartsWith("pull", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DockerThatCannotBeStarted_IsReportedAsADockerRuntimeException()
    {
        var runtime = new CliDockerRuntime(
            _ => throw new System.ComponentModel.Win32Exception(2, "The system cannot find the file specified"),
            new StringWriter());

        await Assert.ThrowsAsync<DockerRuntimeException>(() => runtime.InspectAsync("web"));
    }

    [Fact]
    public async Task InspectAsync_UnreadableOutput_IsReportedAsADockerRuntimeException()
    {
        var (runtime, runner, _) = Create();
        runner.Respond = _ => (0, "not json", string.Empty);

        await Assert.ThrowsAsync<DockerRuntimeException>(() => runtime.InspectAsync("web"));
    }

    [Fact]
    public async Task InspectAsync_DefaultContainer_HasNoUnreproducedSettings()
    {
        var (runtime, runner, _) = Create();
        runner.Respond = Daemon(DefaultInspect(), ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Empty(inspection!.UnreproducedSettings!);
        Assert.Equal("json-file", inspection.Log!.Driver);
    }

    [Theory]
    [InlineData("HostConfig", "Privileged", "true", "HostConfig.Privileged")]
    [InlineData("HostConfig", "Memory", "536870912", "HostConfig.Memory")]
    [InlineData("HostConfig", "CapAdd", "[\"NET_ADMIN\"]", "HostConfig.CapAdd")]
    [InlineData("HostConfig", "Sysctls", "{\"net.core.somaxconn\":\"1024\"}", "HostConfig.Sysctls")]
    [InlineData("HostConfig", "PidsLimit", "100", "HostConfig.PidsLimit")]
    [InlineData("HostConfig", "ShmSize", "134217728", "HostConfig.ShmSize")]
    [InlineData("HostConfig", "NetworkMode", "\"container:other\"", "HostConfig.NetworkMode")]
    [InlineData("HostConfig", "Init", "true", "HostConfig.Init")]
    [InlineData("Config", "Hostname", "\"custom-host\"", "Config.Hostname")]
    [InlineData("Config", "Tty", "true", "Config.Tty")]
    [InlineData("Config", "StopTimeout", "30", "Config.StopTimeout")]
    public async Task InspectAsync_RunOnlySetting_IsNamed(string section, string field, string json, string expected)
    {
        var (runtime, runner, _) = Create();
        var inspect = DefaultInspect();
        inspect[section]![field] = JsonNode.Parse(json);
        runner.Respond = Daemon(inspect, ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(new[] { expected }, inspection!.UnreproducedSettings);
    }

    [Fact]
    public async Task InspectAsync_NetworkAlias_IsNamed()
    {
        var (runtime, runner, _) = Create();
        var inspect = DefaultInspect();
        inspect["NetworkSettings"]!["Networks"]!["bridge"]!["Aliases"] = new JsonArray("web", ContainerId[..12], "api");
        runner.Respond = Daemon(inspect, ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(new[] { "NetworkSettings.Networks.bridge.Aliases" }, inspection!.UnreproducedSettings);
    }

    // The working directory and user can come from the image or from `docker run`. Equal to the image's own is
    // the image's (and the new image's value is right); different is the container's own and would be lost.
    [Fact]
    public async Task InspectAsync_SettingsEqualToTheImages_AreNotNamed_OverridesAre()
    {
        var (runtime, runner, _) = Create();
        var inspect = DefaultInspect();
        inspect["Config"]!["User"] = "1000:1000";
        runner.Respond = Daemon(inspect, ImageConfig(workingDir: "/app", user: ""));

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(new[] { "Config.User" }, inspection!.UnreproducedSettings);
    }

    // With no image configuration to compare against, a set value cannot be shown to be the image's.
    [Fact]
    public async Task InspectAsync_ImageCannotBeInspected_ImageSuppliableSettingsAreNamed()
    {
        var (runtime, runner, _) = Create();
        runner.Respond = Daemon(DefaultInspect(), imageConfig: null);

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(new[] { "Config.WorkingDir" }, inspection!.UnreproducedSettings);
    }

    [Fact]
    public async Task CreateReplacementAsync_PassesTheLogConfiguration()
    {
        var (runtime, runner, _) = Create();
        var spec = new ContainerCreateSpec(
            Name: "web",
            ImageId: ImageId,
            Labels: new Dictionary<string, string>(),
            Env: Array.Empty<string>(),
            Command: Array.Empty<string>(),
            Entrypoint: Array.Empty<string>(),
            Mounts: Array.Empty<MountSpec>(),
            Ports: Array.Empty<PortSpec>(),
            RestartPolicy: "no",
            Networks: Array.Empty<string>(),
            Log: new LogSpec("local", new Dictionary<string, string> { ["max-size"] = "1m" }));

        await runtime.CreateReplacementAsync(spec);

        Assert.Contains("--log-driver local --log-opt max-size=1m", runner.Calls.Single());
    }
}
