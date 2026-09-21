#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Disclosed gap (S2.22/S2.23): the field mapping below (mount/port/network shape, the anonymous-volume
/// heuristic) is written against documented `docker inspect` JSON shapes, not verified against a live daemon —
/// this sandbox cannot run one. Treat it as unverified until exercised against real Docker.
/// </summary>
public sealed class CliDockerRuntime : IDockerRuntime
{
    private static readonly Regex AnonymousVolumeName = new("^[0-9a-f]{64}$", RegexOptions.Compiled);

    public async Task<ContainerInspection?> InspectAsync(string name)
    {
        var (exitCode, stdout, stderr) = await RunAsync("inspect", name);
        if (exitCode != 0)
        {
            if (stderr.Contains("No such object", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new DockerRuntimeException($"docker inspect {name} failed: {stderr}");
        }

        using var document = JsonDocument.Parse(stdout);
        var element = document.RootElement[0];
        return ParseInspection(element);
    }

    public async Task<string> ResolveImageIdAsync(string imageReference)
    {
        var (localExitCode, localStdout, _) = await RunAsync("image", "inspect", "--format", "{{.Id}}", imageReference);
        if (localExitCode == 0)
        {
            return localStdout.Trim();
        }

        var (pullExitCode, _, pullStderr) = await RunAsync("pull", imageReference);
        if (pullExitCode != 0)
        {
            throw new DockerRuntimeException($"docker pull {imageReference} failed: {pullStderr}");
        }

        var (exitCode, stdout, stderr) = await RunAsync("image", "inspect", "--format", "{{.Id}}", imageReference);
        if (exitCode != 0)
        {
            throw new DockerRuntimeException($"docker image inspect {imageReference} failed after pull: {stderr}");
        }

        return stdout.Trim();
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
                var hostSide = port.HostIp != null ? $"{port.HostIp}:{port.HostPort}" : port.HostPort;
                args.Add("--publish");
                args.Add($"{hostSide}:{port.ContainerPort}/{port.Protocol}");
            }
            else
            {
                args.Add("--expose");
                args.Add($"{port.ContainerPort}/{port.Protocol}");
            }
        }

        args.Add("--restart");
        args.Add(spec.RestartPolicy);

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
        if (mount.Source != null)
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

        return new ContainerInspection(
            Id: element.GetProperty("Id").GetString()!,
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
            Ports: ReadPorts(element),
            RestartPolicy: hostConfig.TryGetProperty("RestartPolicy", out var restartPolicy) &&
                           restartPolicy.TryGetProperty("Name", out var restartName)
                ? restartName.GetString() ?? "no"
                : "no",
            Networks: ReadNetworks(element),
            Links: ReadStringArray(hostConfig, "Links"));
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

            result.Add(new MountSpec(kind, source ?? name, destination, readOnly, isAnonymous));
        }

        return result;
    }

    private static IReadOnlyList<PortSpec> ReadPorts(JsonElement element)
    {
        if (!element.TryGetProperty("NetworkSettings", out var networkSettings) ||
            !networkSettings.TryGetProperty("Ports", out var ports) ||
            ports.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<PortSpec>();
        }

        var result = new List<PortSpec>();
        foreach (var entry in ports.EnumerateObject())
        {
            var parts = entry.Name.Split('/');
            var containerPort = int.Parse(parts[0]);
            var protocol = parts.Length > 1 ? parts[1] : "tcp";

            if (entry.Value.ValueKind != JsonValueKind.Array || entry.Value.GetArrayLength() == 0)
            {
                result.Add(new PortSpec(containerPort, protocol, null, null));
                continue;
            }

            foreach (var binding in entry.Value.EnumerateArray())
            {
                var hostIp = binding.TryGetProperty("HostIp", out var hostIpProp) ? hostIpProp.GetString() : null;
                var hostPort = binding.TryGetProperty("HostPort", out var hostPortProp) ? hostPortProp.GetString() : null;
                result.Add(new PortSpec(containerPort, protocol, hostIp, hostPort));
            }
        }

        return result;
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

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(params string[] args)
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
