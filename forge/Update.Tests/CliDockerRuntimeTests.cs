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
    [InlineData("HostConfig", "ReadonlyRootfs", "true", "HostConfig.ReadonlyRootfs")]
    [InlineData("HostConfig", "CpuShares", "512", "HostConfig.CpuShares")]
    [InlineData("HostConfig", "CpuQuota", "50000", "HostConfig.CpuQuota")]
    [InlineData("HostConfig", "CapAdd", "[\"NET_ADMIN\"]", "HostConfig.CapAdd")]
    [InlineData("HostConfig", "Sysctls", "{\"net.core.somaxconn\":\"1024\"}", "HostConfig.Sysctls")]
    [InlineData("HostConfig", "PidsLimit", "100", "HostConfig.PidsLimit")]
    [InlineData("HostConfig", "ShmSize", "134217728", "HostConfig.ShmSize")]
    [InlineData("HostConfig", "NetworkMode", "\"container:other\"", "HostConfig.NetworkMode")]
    [InlineData("HostConfig", "Init", "true", "HostConfig.Init")]
    [InlineData("Config", "Hostname", "\"custom-host\"", "Config.Hostname")]
    [InlineData("Config", "Tty", "true", "Config.Tty")]
    public async Task InspectAsync_RunOnlySetting_IsNamed(string section, string field, string json, string expected)
    {
        var (runtime, runner, _) = Create();
        var inspect = DefaultInspect();
        inspect[section]![field] = JsonNode.Parse(json);
        runner.Respond = Daemon(inspect, ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(new[] { expected }, inspection!.UnreproducedSettings);
    }

    // A runner container's shape: privileged, CPU and memory limits, a /tmp tmpfs, a stop timeout, and an alias on its network.
    private static JsonObject RunnerInspect()
    {
        var inspect = DefaultInspect();
        var host = inspect["HostConfig"]!;
        host["Privileged"] = true;
        host["SecurityOpt"] = new JsonArray("label=disable"); // what the daemon adds for --privileged
        host["Memory"] = 8589934592;
        host["MemorySwap"] = 17179869184;
        host["MemoryReservation"] = 4294967296;
        host["NanoCpus"] = 2500000000;
        host["Tmpfs"] = new JsonObject { ["/tmp"] = "", ["/run"] = "rw,size=64m" };
        inspect["Config"]!["StopTimeout"] = 60;
        inspect["NetworkSettings"]!["Networks"]!["bridge"]!["Aliases"] = new JsonArray("web", ContainerId[..12], "docker");
        return inspect;
    }

    [Fact]
    public async Task InspectAsync_RunnerSettings_AreReadAndNotRefused()
    {
        var (runtime, runner, _) = Create();
        runner.Respond = Daemon(RunnerInspect(), ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Empty(inspection!.UnreproducedSettings!);
        Assert.Equal(new RuntimeLimits(true, 8589934592, 17179869184, 4294967296, 2500000000), inspection.Limits);
        Assert.Equal(new Dictionary<string, string> { ["/tmp"] = "", ["/run"] = "rw,size=64m" }, inspection.Tmpfs);
        Assert.Equal(60, inspection.StopTimeoutSeconds);
        // The daemon's own aliases (the container's name and the start of its id) are not the caller's.
        Assert.Equal(new[] { "docker" }, inspection.NetworkAliases!["bridge"]);
    }

    // A user alias is the user's however it looks: "0", "012" and "0123456789ab" are all starts of this container's id.
    // Only the exact aliases the daemon adds of its own (the container's name, the 12-character short id, the id itself)
    // are dropped, and each alias stays on the network it was set on.
    [Fact]
    public async Task InspectAsync_AliasesThatStartLikeTheId_AreKeptOnTheirNetworksThroughReplacement()
    {
        var (runtime, runner, _) = Create();
        var inspect = DefaultInspect();
        inspect["NetworkSettings"]!["Networks"] = new JsonObject
        {
            ["runner-net"] = new JsonObject
            {
                ["IPAMConfig"] = null,
                ["DriverOpts"] = null,
                ["Aliases"] = new JsonArray("web", "0", "012", "api", ContainerId[..12], ContainerId),
            },
            ["backend"] = new JsonObject
            {
                ["IPAMConfig"] = null,
                ["DriverOpts"] = null,
                ["Aliases"] = new JsonArray(ContainerId[..12], "0123456789abc", "db", "web"),
            },
        };
        runner.Respond = Daemon(inspect, ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Empty(inspection!.UnreproducedSettings!);
        Assert.Equal(new[] { "0", "012", "api" }, inspection.NetworkAliases!["runner-net"]);
        Assert.Equal(new[] { "0123456789abc", "db" }, inspection.NetworkAliases["backend"]);

        // What the replacement is then created with: the same aliases, each on its own network.
        runner.Calls.Clear();
        await runtime.CreateReplacementAsync(Spec(
            networks: inspection.Networks,
            aliases: inspection.NetworkAliases));

        Assert.Equal(
            new[]
            {
                $"create --name web --restart unless-stopped --network runner-net --network-alias 0 --network-alias 012 --network-alias api {ImageId} serve",
                "network connect --alias 0123456789abc --alias db backend web",
            },
            runner.Calls);
    }

    [Theory]
    [InlineData(true, "[\"label=disable\",\"seccomp=unconfined\"]", true)]
    [InlineData(false, "[\"label=disable\"]", true)]
    [InlineData(true, "[\"apparmor=custom\"]", true)]
    [InlineData(true, "[\"label=disable\"]", false)]
    public async Task InspectAsync_SecurityOptions_OnlyTheOnesPrivilegedImpliesAreForgiven(bool privileged, string securityOpt, bool named)
    {
        var (runtime, runner, _) = Create();
        var inspect = DefaultInspect();
        inspect["HostConfig"]!["Privileged"] = privileged;
        inspect["HostConfig"]!["SecurityOpt"] = JsonNode.Parse(securityOpt);
        runner.Respond = Daemon(inspect, ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(named ? new[] { "HostConfig.SecurityOpt" } : Array.Empty<string>(), inspection!.UnreproducedSettings);
    }

    [Fact]
    public async Task InspectAsync_DefaultContainer_CarriesNoRunnerSettings()
    {
        var (runtime, runner, _) = Create();
        runner.Respond = Daemon(DefaultInspect(), ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(RuntimeLimits.None, inspection!.Limits);
        Assert.Empty(inspection.Tmpfs!);
        Assert.Empty(inspection.NetworkAliases!);
        Assert.Null(inspection.StopTimeoutSeconds);
    }

    // A swap limit with no memory limit (--memory-swap -1) is not something --memory-swap can state on its own.
    [Fact]
    public async Task InspectAsync_SwapLimitWithoutMemoryLimit_IsNamed()
    {
        var (runtime, runner, _) = Create();
        var inspect = DefaultInspect();
        inspect["HostConfig"]!["MemorySwap"] = -1;
        runner.Respond = Daemon(inspect, ImageConfig());

        var inspection = await runtime.InspectAsync("web");

        Assert.Equal(new[] { "HostConfig.MemorySwap" }, inspection!.UnreproducedSettings);
    }

    private static ContainerCreateSpec Spec(
        IReadOnlyList<string>? networks = null,
        RuntimeLimits? limits = null,
        IReadOnlyDictionary<string, string>? tmpfs = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? aliases = null,
        int? stopTimeout = null) =>
        new(
            Name: "web",
            ImageId: ImageId,
            Labels: new Dictionary<string, string>(),
            Env: Array.Empty<string>(),
            Command: new[] { "serve" },
            Entrypoint: Array.Empty<string>(),
            Mounts: Array.Empty<MountSpec>(),
            Ports: Array.Empty<PortSpec>(),
            RestartPolicy: "unless-stopped",
            Networks: networks ?? Array.Empty<string>(),
            Limits: limits,
            Tmpfs: tmpfs,
            NetworkAliases: aliases,
            StopTimeoutSeconds: stopTimeout);

    [Fact]
    public async Task CreateReplacementAsync_PassesRunnerSettings()
    {
        var (runtime, runner, _) = Create();
        var spec = Spec(
            networks: new[] { "runner-net" },
            limits: new RuntimeLimits(true, 8589934592, 17179869184, 4294967296, 2500000000),
            tmpfs: new Dictionary<string, string> { ["/tmp"] = "", ["/run"] = "rw,size=64m" },
            aliases: new Dictionary<string, IReadOnlyList<string>> { ["runner-net"] = new[] { "docker" } },
            stopTimeout: 60);

        await runtime.CreateReplacementAsync(spec);

        Assert.Equal(
            "create --name web --restart unless-stopped --privileged --memory 8589934592 --memory-swap 17179869184 " +
            "--memory-reservation 4294967296 --cpus 2.5 --tmpfs /tmp --tmpfs /run:rw,size=64m --stop-timeout 60 " +
            $"--network runner-net --network-alias docker {ImageId} serve",
            runner.Calls.Single());
    }

    [Fact]
    public async Task CreateReplacementAsync_NoRunnerSettings_AddsNoneOfTheirFlags()
    {
        var (runtime, runner, _) = Create();

        await runtime.CreateReplacementAsync(Spec(limits: RuntimeLimits.None));

        Assert.Equal($"create --name web --restart unless-stopped {ImageId} serve", runner.Calls.Single());
    }

    // Aliases are per network: the first network takes them with --network-alias, later ones with `network connect --alias`.
    [Fact]
    public async Task CreateReplacementAsync_AliasesOnLaterNetworks_AreSetWhenConnecting()
    {
        var (runtime, runner, _) = Create();
        var spec = Spec(
            networks: new[] { "first", "second", "third" },
            aliases: new Dictionary<string, IReadOnlyList<string>>
            {
                ["first"] = new[] { "a" },
                ["second"] = new[] { "b", "c" },
            });

        await runtime.CreateReplacementAsync(spec);

        Assert.Equal(
            new[]
            {
                $"create --name web --restart unless-stopped --network first --network-alias a {ImageId} serve",
                "network connect --alias b --alias c second web",
                "network connect third web",
            },
            runner.Calls);
    }

    // The container that the daemon derived swap for (twice the memory) is passed back as it reports it.
    [Fact]
    public async Task CreateReplacementAsync_MemoryWithoutSwap_OmitsMemorySwap()
    {
        var (runtime, runner, _) = Create();

        await runtime.CreateReplacementAsync(Spec(limits: new RuntimeLimits(Memory: 536870912)));

        Assert.Equal($"create --name web --restart unless-stopped --memory 536870912 {ImageId} serve", runner.Calls.Single());
    }

    [Fact]
    public async Task CreateReplacementAsync_FractionalCpus_AreFormattedWithoutCultureOrRounding()
    {
        var (runtime, runner, _) = Create();
        var current = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            await runtime.CreateReplacementAsync(Spec(limits: new RuntimeLimits(NanoCpus: 1500000000)));
            await runtime.CreateReplacementAsync(Spec(limits: new RuntimeLimits(NanoCpus: 100000000)));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = current;
        }

        Assert.Contains("--cpus 1.5 ", runner.Calls[0]);
        Assert.Contains("--cpus 0.1 ", runner.Calls[1]);
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
