#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Update;

/// <summary>
/// Shells to the `docker` CLI on the caller's PATH. Arguments are passed through
/// <see cref="ProcessStartInfo.ArgumentList"/> element by element, never through a shell string, so a container
/// name or label value cannot break out of its argument.
///
/// S2.22: the field mapping below (mount/port/network shape, the anonymous-volume heuristic) is verified
/// against a live daemon by <c>Update.Tests.RoundTripProbeTests</c>, which round-trips each shape through
/// <see cref="InspectAsync"/> and <see cref="CreateReplacementAsync"/> the same way <c>Updater</c> does. That
/// suite runs wherever a `docker` daemon is reachable — CI on Linux, and locally on a Windows host running
/// Docker Desktop, whose daemon is itself a Linux VM — and skips itself otherwise. S2.23: a daemon running
/// Windows containers natively remains a stated, unverified gap for live-daemon update behaviour specifically,
/// distinct from the argument translation and path/mount handling covered on Windows already.
/// </summary>
public sealed class CliDockerRuntime : IDockerRuntime
{
    private static readonly Regex AnonymousVolumeName = new("^[0-9a-f]{64}$", RegexOptions.Compiled);
    private static readonly Regex ImageIdReference = new("^(sha256:)?[0-9a-f]{64}$", RegexOptions.Compiled);

    private readonly Func<IReadOnlyList<string>, Task<(int ExitCode, string Stdout, string Stderr)>> _run;
    private readonly TextWriter _warnings;

    // An image's configuration never changes under its id, so each is read from the daemon once.
    private readonly Dictionary<string, JsonElement?> _imageConfigs = new(StringComparer.Ordinal);

    public CliDockerRuntime()
        : this(RunProcessAsync, Console.Error)
    {
    }

    /// <summary>Takes the process runner and the warning writer as seams, so tests can drive the exact argument
    /// sequences and inspect output without a daemon.</summary>
    internal CliDockerRuntime(Func<IReadOnlyList<string>, Task<(int ExitCode, string Stdout, string Stderr)>> run,
        TextWriter warnings)
    {
        _run = run;
        _warnings = warnings;
    }

    public async Task<ContainerInspection?> InspectAsync(string name)
    {
        // --type container: an image, network or volume sharing the name must read as "no container", not as
        // JSON of a different shape.
        var (exitCode, stdout, stderr) = await RunAsync("inspect", "--type", "container", name);
        if (exitCode != 0)
        {
            if (stderr.Contains("No such container", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("No such object", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new DockerRuntimeException($"docker inspect {name} failed: {stderr}");
        }

        ContainerInspection inspection;
        JsonElement config;
        try
        {
            using var document = JsonDocument.Parse(stdout);
            var element = document.RootElement[0];
            inspection = ParseInspection(element);
            config = element.GetProperty("Config").Clone();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException)
        {
            // Output this mapping cannot read is a daemon-level failure like any other, not a crash that would
            // skip the Updater's restore and lock release.
            throw new DockerRuntimeException($"docker inspect {name} returned output that could not be read: {ex.Message}", ex);
        }

        var imageSet = await FindImageOverridesAsync(inspection.ImageId, config);
        if (imageSet.Count == 0)
        {
            return inspection;
        }

        return inspection with { UnreproducedSettings = (inspection.UnreproducedSettings ?? Array.Empty<string>()).Concat(imageSet).ToList() };
    }

    /// <summary>Pulls <paramref name="imageReference"/> before reading its id, so a tag that moved in the registry
    /// is picked up even when an older image under the same tag is already local (contract § Global tool:
    /// "resolved afresh against the registry"). An image id is immutable and only ever local, so it is never
    /// pulled. When the pull fails and a local image answers to the reference — an image built on this host, or a
    /// registry that cannot be reached — that image is used and a warning says it may be out of date.</summary>
    public async Task<string> ResolveImageIdAsync(string imageReference)
    {
        if (ImageIdReference.IsMatch(imageReference))
        {
            return await LocalImageIdAsync(imageReference)
                ?? throw new DockerRuntimeException($"Image '{imageReference}' is not present locally.");
        }

        var (pullExitCode, _, pullStderr) = await RunAsync("pull", imageReference);
        if (pullExitCode == 0)
        {
            return await LocalImageIdAsync(imageReference)
                ?? throw new DockerRuntimeException($"docker image inspect {imageReference} failed after pull.");
        }

        var localId = await LocalImageIdAsync(imageReference);
        if (localId == null)
        {
            throw new DockerRuntimeException($"docker pull {imageReference} failed: {pullStderr}");
        }

        await _warnings.WriteLineAsync(
            $"Warning: '{imageReference}' could not be pulled ({pullStderr.Trim()}); using the local image, which may be out of date.");
        return localId;
    }

    private async Task<string?> LocalImageIdAsync(string imageReference)
    {
        var (exitCode, stdout, _) = await RunAsync("image", "inspect", "--format", "{{.Id}}", imageReference);
        return exitCode == 0 ? stdout.Trim() : null;
    }

    public async Task<string> CreateMarkerAsync(string name, string imageId, IReadOnlyDictionary<string, string> labels)
    {
        var args = new List<string> { "create", "--name", name };
        foreach (var (key, value) in labels)
        {
            args.Add("--label");
            args.Add($"{key}={value}");
        }

        args.Add(imageId);

        var (exitCode, stdout, stderr) = await RunAsync(args.ToArray());
        if (exitCode != 0)
        {
            throw new DockerRuntimeException($"docker create {name} failed: {stderr}");
        }

        return stdout.Trim();
    }

    public async Task<string> CreateReplacementAsync(ContainerCreateSpec spec)
    {
        var args = new List<string> { "create", "--name", spec.Name };

        foreach (var (key, value) in spec.Labels)
        {
            args.Add("--label");
            args.Add($"{key}={value}");
        }

        foreach (var entry in spec.Env)
        {
            args.Add("--env");
            args.Add(entry);
        }

        if (spec.Entrypoint.Count > 0)
        {
            args.Add("--entrypoint");
            args.Add(spec.Entrypoint[0]);
        }

        foreach (var mount in spec.Mounts)
        {
            args.Add("--mount");
            args.Add(FormatMount(mount));
        }

        foreach (var port in spec.Ports)
        {
            if (port.HostPort != null)
            {
                // An empty HostPort is a publish to a daemon-chosen host port, as `-p 80` requested it.
                var hostSide = port.HostIp != null ? $"{port.HostIp}:{port.HostPort}:" : port.HostPort.Length > 0 ? $"{port.HostPort}:" : string.Empty;
                args.Add("--publish");
                args.Add($"{hostSide}{port.ContainerPort}/{port.Protocol}");
            }
            else
            {
                args.Add("--expose");
                args.Add($"{port.ContainerPort}/{port.Protocol}");
            }
        }

        args.Add("--restart");
        args.Add(spec.RestartPolicy);

        if (spec.Log != null)
        {
            args.Add("--log-driver");
            args.Add(spec.Log.Driver);
            foreach (var (key, value) in spec.Log.Options)
            {
                args.Add("--log-opt");
                args.Add($"{key}={value}");
            }
        }

        if (spec.Networks.Count > 0)
        {
            args.Add("--network");
            args.Add(spec.Networks[0]);
        }

        args.Add(spec.ImageId);

        if (spec.Entrypoint.Count > 1)
        {
            args.AddRange(spec.Entrypoint.Skip(1));
        }

        args.AddRange(spec.Command);

        var (exitCode, stdout, stderr) = await RunAsync(args.ToArray());
        if (exitCode != 0)
        {
            throw new DockerRuntimeException($"docker create {spec.Name} failed: {stderr}");
        }

        var id = stdout.Trim();

        foreach (var network in spec.Networks.Skip(1))
        {
            var (connectExitCode, _, connectStderr) = await RunAsync("network", "connect", network, spec.Name);
            if (connectExitCode != 0)
            {
                throw new DockerRuntimeException($"docker network connect {network} {spec.Name} failed: {connectStderr}");
            }
        }

        return id;
    }

    public async Task StartAsync(string name) => await RunChecked("docker start", "start", name);

    public async Task StopAsync(string name) => await RunChecked("docker stop", "stop", name);

    public async Task RenameAsync(string currentName, string newName) =>
        await RunChecked("docker rename", "rename", currentName, newName);

    public async Task RemoveAsync(string name, bool force)
    {
        var args = force ? new[] { "rm", "--force", name } : new[] { "rm", name };
        await RunChecked("docker rm", args);
    }

    public async Task TagImageAsync(string imageId, string tag) =>
        await RunChecked("docker tag", "tag", imageId, tag);

    public async Task RemoveImageTagAsync(string tag) =>
        await RunChecked("docker rmi", "rmi", tag);

    private async Task RunChecked(string label, params string[] args)
    {
        var (exitCode, _, stderr) = await RunAsync(args);
        if (exitCode != 0)
        {
            throw new DockerRuntimeException($"{label} failed: {stderr}");
        }
    }

    private static string FormatMount(MountSpec mount)
    {
        var kind = mount.Kind switch
        {
            MountKind.Bind => "type=bind",
            MountKind.Volume => "type=volume",
            MountKind.Tmpfs => "type=tmpfs",
            _ => throw new ArgumentOutOfRangeException(nameof(mount)),
        };

        var parts = new List<string> { kind };
        if (!string.IsNullOrEmpty(mount.Source) && mount.Kind != MountKind.Tmpfs)
        {
            parts.Add($"source={mount.Source}");
        }

        parts.Add($"destination={mount.Destination}");
        if (mount.ReadOnly)
        {
            parts.Add("readonly");
        }

        return string.Join(",", parts);
    }

    private static ContainerInspection ParseInspection(JsonElement element)
    {
        var config = element.GetProperty("Config");
        var hostConfig = element.GetProperty("HostConfig");
        var state = element.GetProperty("State");

        var labels = ReadStringMap(config, "Labels");
        var name = element.GetProperty("Name").GetString()!.TrimStart('/');
        var imageId = element.GetProperty("Image").GetString()!;
        var imageReference = config.TryGetProperty("Image", out var imageRefProp) ? imageRefProp.GetString() : null;

        var id = element.GetProperty("Id").GetString()!;

        return new ContainerInspection(
            Id: id,
            Name: name,
            ImageId: imageId,
            ImageReference: imageReference,
            Running: state.GetProperty("Running").GetBoolean(),
            AutoRemove: hostConfig.TryGetProperty("AutoRemove", out var autoRemove) && autoRemove.GetBoolean(),
            Labels: labels,
            Env: ReadStringArray(config, "Env"),
            Command: ReadStringArray(config, "Cmd"),
            Entrypoint: ReadStringArray(config, "Entrypoint"),
            Mounts: ReadMounts(element),
            Ports: ReadPorts(config, hostConfig),
            RestartPolicy: ReadRestartPolicy(hostConfig),
            Networks: ReadNetworks(element),
            Links: ReadStringArray(hostConfig, "Links"),
            HealthStatus: ReadHealthStatus(state),
            Log: ReadLog(hostConfig),
            UnreproducedSettings: FindUnreproducedSettings(element, id));
    }

    private static LogSpec? ReadLog(JsonElement hostConfig)
    {
        if (!hostConfig.TryGetProperty("LogConfig", out var logConfig) || logConfig.ValueKind != JsonValueKind.Object ||
            !logConfig.TryGetProperty("Type", out var type) || string.IsNullOrEmpty(type.GetString()))
        {
            return null;
        }

        return new LogSpec(type.GetString()!, ReadStringMap(logConfig, "Config"));
    }

    // Run-time settings that `CreateReplacementAsync` does not pass to `docker create`, grouped by the value a
    // container created without the option reports. A container carrying any other value is refused (I34)
    // rather than replaced by one that silently lacks it.
    private static readonly string[] UnreproducedFlags =
    {
        "Privileged", "PublishAllPorts", "ReadonlyRootfs", "OomKillDisable", "Init",
    };

    private static readonly string[] UnreproducedLists =
    {
        "CapAdd", "CapDrop", "Dns", "DnsOptions", "DnsSearch", "ExtraHosts", "GroupAdd", "SecurityOpt", "Devices",
        "DeviceCgroupRules", "DeviceRequests", "Ulimits", "VolumesFrom", "BlkioWeightDevice", "BlkioDeviceReadBps",
        "BlkioDeviceWriteBps", "BlkioDeviceReadIOps", "BlkioDeviceWriteIOps",
    };

    private static readonly string[] UnreproducedMaps = { "Sysctls", "Tmpfs", "StorageOpt", "Annotations" };

    private static readonly string[] UnreproducedNumbers =
    {
        "Memory", "MemoryReservation", "MemorySwap", "NanoCpus", "CpuShares", "CpuPeriod", "CpuQuota",
        "CpuRealtimePeriod", "CpuRealtimeRuntime", "CpuCount", "CpuPercent", "BlkioWeight", "OomScoreAdj",
        "IOMaximumIOps", "IOMaximumBandwidth",
    };

    private static readonly string[] UnreproducedStrings =
    {
        "CpusetCpus", "CpusetMems", "CgroupParent", "Cgroup", "PidMode", "UTSMode", "UsernsMode", "VolumeDriver",
    };

    /// <summary>Names every setting of the inspected container that a replacement would not carry (I34), as
    /// <c>HostConfig.&lt;field&gt;</c>, <c>Config.&lt;field&gt;</c>, <c>Mounts.&lt;destination&gt;.Propagation</c> or
    /// <c>NetworkSettings.Networks.&lt;network&gt;.&lt;field&gt;</c>. Settings an image can also supply (user, working
    /// directory, stop signal, health check) are compared against the image separately, in
    /// <see cref="FindImageOverridesAsync"/>.</summary>
    internal static IReadOnlyList<string> FindUnreproducedSettings(JsonElement element, string id)
    {
        var found = new List<string>();
        var hostConfig = element.GetProperty("HostConfig");
        var config = element.GetProperty("Config");

        foreach (var field in UnreproducedFlags)
        {
            if (hostConfig.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.True)
            {
                found.Add($"HostConfig.{field}");
            }
        }

        foreach (var field in UnreproducedLists)
        {
            if (hostConfig.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0)
            {
                found.Add($"HostConfig.{field}");
            }
        }

        foreach (var field in UnreproducedMaps)
        {
            if (hostConfig.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Any())
            {
                found.Add($"HostConfig.{field}");
            }
        }

        foreach (var field in UnreproducedNumbers)
        {
            if (hostConfig.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number && value.GetDouble() != 0)
            {
                found.Add($"HostConfig.{field}");
            }
        }

        foreach (var field in UnreproducedStrings)
        {
            if (hostConfig.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 0)
            {
                found.Add($"HostConfig.{field}");
            }
        }

        // Null, 0 and -1 all mean "no limit".
        if (hostConfig.TryGetProperty("PidsLimit", out var pidsLimit) && pidsLimit.ValueKind == JsonValueKind.Number && pidsLimit.GetInt64() > 0)
        {
            found.Add("HostConfig.PidsLimit");
        }

        // Null and -1 both mean "inherit the host's".
        if (hostConfig.TryGetProperty("MemorySwappiness", out var swappiness) && swappiness.ValueKind == JsonValueKind.Number && swappiness.GetInt64() >= 0)
        {
            found.Add("HostConfig.MemorySwappiness");
        }

        // 64 MiB is the daemon's own default.
        if (hostConfig.TryGetProperty("ShmSize", out var shmSize) && shmSize.ValueKind == JsonValueKind.Number &&
            shmSize.GetInt64() != 0 && shmSize.GetInt64() != 64L * 1024 * 1024)
        {
            found.Add("HostConfig.ShmSize");
        }

        if (ReadString(hostConfig, "IpcMode") is { Length: > 0 } ipcMode && ipcMode != "private" && ipcMode != "shareable")
        {
            found.Add("HostConfig.IpcMode");
        }

        if (ReadString(hostConfig, "Runtime") is { Length: > 0 } runtime && runtime != "runc")
        {
            found.Add("HostConfig.Runtime");
        }

        // The replacement joins its networks by name; sharing another container's network stack is not a name.
        if (ReadString(hostConfig, "NetworkMode")?.StartsWith("container:", StringComparison.Ordinal) == true)
        {
            found.Add("HostConfig.NetworkMode");
        }

        // A container created without --hostname is named after its own short id.
        if (ReadString(config, "Hostname") is { Length: > 0 } hostname && !id.StartsWith(hostname, StringComparison.Ordinal))
        {
            found.Add("Config.Hostname");
        }

        if (ReadString(config, "Domainname") is { Length: > 0 })
        {
            found.Add("Config.Domainname");
        }

        foreach (var field in new[] { "Tty", "OpenStdin", "StdinOnce" })
        {
            if (config.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.True)
            {
                found.Add($"Config.{field}");
            }
        }

        if (config.TryGetProperty("StopTimeout", out var stopTimeout) && stopTimeout.ValueKind == JsonValueKind.Number)
        {
            found.Add("Config.StopTimeout");
        }

        if (element.TryGetProperty("Mounts", out var mounts) && mounts.ValueKind == JsonValueKind.Array)
        {
            foreach (var mount in mounts.EnumerateArray())
            {
                if (ReadString(mount, "Propagation") is { Length: > 0 } propagation && propagation != "rprivate")
                {
                    found.Add($"Mounts.{ReadString(mount, "Destination")}.Propagation");
                }
            }
        }

        if (element.TryGetProperty("NetworkSettings", out var networkSettings) &&
            networkSettings.TryGetProperty("Networks", out var networks) && networks.ValueKind == JsonValueKind.Object)
        {
            var name = element.GetProperty("Name").GetString()!.TrimStart('/');
            foreach (var network in networks.EnumerateObject())
            {
                var endpoint = network.Value;
                if (endpoint.TryGetProperty("IPAMConfig", out var ipam) && ipam.ValueKind == JsonValueKind.Object &&
                    ipam.EnumerateObject().Any(p => p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString()!.Length > 0,
                        JsonValueKind.Array => p.Value.GetArrayLength() > 0,
                        _ => false,
                    }))
                {
                    found.Add($"NetworkSettings.Networks.{network.Name}.IPAMConfig");
                }

                // Some daemons list the container's own short id and name as aliases of their own accord.
                if (endpoint.TryGetProperty("Aliases", out var aliases) && aliases.ValueKind == JsonValueKind.Array &&
                    aliases.EnumerateArray().Select(a => a.GetString() ?? string.Empty)
                        .Any(a => a.Length > 0 && a != name && !id.StartsWith(a, StringComparison.Ordinal)))
                {
                    found.Add($"NetworkSettings.Networks.{network.Name}.Aliases");
                }

                if (endpoint.TryGetProperty("DriverOpts", out var driverOpts) && driverOpts.ValueKind == JsonValueKind.Object &&
                    driverOpts.EnumerateObject().Any())
                {
                    found.Add($"NetworkSettings.Networks.{network.Name}.DriverOpts");
                }
            }
        }

        return found;
    }

    private static string? ReadString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    // Settings an image can supply as well as `docker run`. A value equal to the image's own came from the image,
    // and the new image's value is the right one to take; any other value was set for this container and the
    // replacement would lose it.
    private static readonly string[] ImageSuppliedSettings = { "User", "WorkingDir", "StopSignal", "Healthcheck" };

    private async Task<IReadOnlyList<string>> FindImageOverridesAsync(string imageId, JsonElement config)
    {
        var set = ImageSuppliedSettings
            .Where(field => config.TryGetProperty(field, out var value) && !IsUnset(value))
            .ToList();
        if (set.Count == 0)
        {
            return set;
        }

        var imageConfig = await ImageConfigAsync(imageId);
        return set
            .Where(field => imageConfig is not { } image ||
                            !image.TryGetProperty(field, out var imageValue) ||
                            config.GetProperty(field).GetRawText() != imageValue.GetRawText())
            .Select(field => $"Config.{field}")
            .ToList();
    }

    private static bool IsUnset(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => true,
        JsonValueKind.String => value.GetString()!.Length == 0,
        _ => false,
    };

    /// <summary>The image's own <c>Config</c>, or null when the daemon cannot report it — in which case every
    /// image-suppliable setting the container carries counts as its own, so the update refuses rather than
    /// guessing.</summary>
    private async Task<JsonElement?> ImageConfigAsync(string imageId)
    {
        if (_imageConfigs.TryGetValue(imageId, out var cached))
        {
            return cached;
        }

        JsonElement? result = null;
        var (exitCode, stdout, _) = await RunAsync("image", "inspect", "--format", "{{json .Config}}", imageId);
        if (exitCode == 0)
        {
            try
            {
                using var document = JsonDocument.Parse(stdout);
                result = document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
            }
            catch (JsonException)
            {
            }
        }

        _imageConfigs[imageId] = result;
        return result;
    }

    /// <summary>Null when the container declares no <c>HEALTHCHECK</c> at all — Docker omits <c>State.Health</c>
    /// entirely in that case, distinct from a declared check that has not yet reported (which starts "starting").</summary>
    private static string? ReadHealthStatus(JsonElement state) =>
        state.TryGetProperty("Health", out var health) && health.ValueKind == JsonValueKind.Object &&
        health.TryGetProperty("Status", out var status)
            ? status.GetString()
            : null;

    private static string ReadRestartPolicy(JsonElement hostConfig)
    {
        if (!hostConfig.TryGetProperty("RestartPolicy", out var restartPolicy) ||
            !restartPolicy.TryGetProperty("Name", out var restartName) ||
            string.IsNullOrEmpty(restartName.GetString()))
        {
            return "no";
        }

        var name = restartName.GetString()!;
        return name == "on-failure" &&
               restartPolicy.TryGetProperty("MaximumRetryCount", out var retries) &&
               retries.ValueKind == JsonValueKind.Number &&
               retries.GetInt32() > 0
            ? $"{name}:{retries.GetInt32()}"
            : name;
    }

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }

        return property.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return property.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();
    }

    private static IReadOnlyList<MountSpec> ReadMounts(JsonElement element)
    {
        if (!element.TryGetProperty("Mounts", out var mounts) || mounts.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MountSpec>();
        }

        var result = new List<MountSpec>();
        foreach (var mount in mounts.EnumerateArray())
        {
            var typeText = mount.GetProperty("Type").GetString() ?? "bind";
            var kind = typeText switch
            {
                "volume" => MountKind.Volume,
                "tmpfs" => MountKind.Tmpfs,
                _ => MountKind.Bind,
            };

            var name = mount.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() : null;
            var source = mount.TryGetProperty("Source", out var sourceProp) ? sourceProp.GetString() : null;
            var destination = mount.GetProperty("Destination").GetString()!;
            var readOnly = mount.TryGetProperty("RW", out var rw) && !rw.GetBoolean();

            // Best-effort (S2.22): Docker names an anonymous volume with its own generated 64-hex-char id;
            // a caller-named volume is not verified against a live daemon here.
            var isAnonymous = kind == MountKind.Volume && name != null && AnonymousVolumeName.IsMatch(name);

            // A volume is addressed by its name; its Source is the daemon's host path, which `--mount` rejects.
            result.Add(new MountSpec(kind, kind == MountKind.Volume ? name : source, destination, readOnly, isAnonymous));
        }

        return result;
    }

    /// <summary>Reads the requested bindings (HostConfig.PortBindings), not the runtime ones
    /// (NetworkSettings.Ports): the latter are empty for a stopped container and pin a daemon-chosen host port.
    /// An empty HostPort is kept as "" (publish to any host port); an exposed-only port has a null HostPort.</summary>
    private static IReadOnlyList<PortSpec> ReadPorts(JsonElement config, JsonElement hostConfig)
    {
        var result = new List<PortSpec>();
        var published = new HashSet<string>(StringComparer.Ordinal);

        if (hostConfig.TryGetProperty("PortBindings", out var bindings) && bindings.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in bindings.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Array || entry.Value.GetArrayLength() == 0)
                {
                    continue;
                }

                var (containerPort, protocol) = ParsePortKey(entry.Name);
                published.Add(entry.Name);
                foreach (var binding in entry.Value.EnumerateArray())
                {
                    var hostIp = binding.TryGetProperty("HostIp", out var hostIpProp) ? hostIpProp.GetString() : null;
                    var hostPort = binding.TryGetProperty("HostPort", out var hostPortProp) ? hostPortProp.GetString() : null;
                    result.Add(new PortSpec(containerPort, protocol, string.IsNullOrEmpty(hostIp) ? null : hostIp, hostPort ?? string.Empty));
                }
            }
        }

        if (config.TryGetProperty("ExposedPorts", out var exposed) && exposed.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in exposed.EnumerateObject().Where(e => !published.Contains(e.Name)))
            {
                var (containerPort, protocol) = ParsePortKey(entry.Name);
                result.Add(new PortSpec(containerPort, protocol, null, null));
            }
        }

        return result;
    }

    private static (int ContainerPort, string Protocol) ParsePortKey(string key)
    {
        var parts = key.Split('/');
        return (int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), parts.Length > 1 ? parts[1] : "tcp");
    }

    private static IReadOnlyList<string> ReadNetworks(JsonElement element)
    {
        if (!element.TryGetProperty("NetworkSettings", out var networkSettings) ||
            !networkSettings.TryGetProperty("Networks", out var networks) ||
            networks.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        return networks.EnumerateObject().Select(p => p.Name).ToList();
    }

    /// <summary>Runs one `docker` command. A `docker` that cannot be started at all is reported as a
    /// <see cref="DockerRuntimeException"/> like any daemon failure, so callers see one exception type for it.</summary>
    private async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(params string[] args)
    {
        try
        {
            return await _run(args);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new DockerRuntimeException($"docker {string.Join(' ', args.Take(2))} could not be run: {ex.Message}", ex);
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(IReadOnlyList<string> args)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
