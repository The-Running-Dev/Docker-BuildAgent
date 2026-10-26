#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Launcher;

/// <summary>
/// Translates `build-agent build &lt;type&gt; [args]...` on the host into one `docker run` of the versioned image
/// (design/10-design.md § Control flow 1). It depends on the image only through its published invocation surface:
/// the `build` command, the `/workspace` mount point and the parameter environment names.
/// </summary>
public static class BuildInvocation
{
    public const string ImageRepository = "ghcr.io/the-running-dev/build-agent";

    /// <summary>Names a full image reference to run instead of the tool's own version.</summary>
    public const string ImageOverrideVariable = "BUILD_AGENT_IMAGE";

    public const string DefaultDockerHost = "unix:///var/run/docker.sock";

    /// <summary>I29: the closed set every entry point accepts; no entry point defaults the type.</summary>
    public static readonly IReadOnlyList<string> AcceptedBuildTypes = new[]
    {
        "docker", "node", "node-in-docker", "node-template", "forge",
    };

    /// <summary>
    /// The environment variable name of every build parameter that has one, as documentation/docs/parameters.md
    /// publishes them. Each is forwarded by name when set on the host, so `RegistryToken` in a CI step's
    /// environment reaches the build as it would in a job running inside the image.
    /// </summary>
    public static readonly IReadOnlyList<string> ParameterEnvironmentNames = new[]
    {
        "ArtifactsDir", "ChangeLogSource", "CreateGitHubRelease", "DockerFile", "DryRun", "ForceNotifications",
        "ForcePush", "ImageTag", "Notifications", "NotificationsWebHookUrl", "PreRelease", "RegistryToken",
        "RegistryUrl", "RegistryUser", "TemplatesDir", "Verbosity",
    };

    /// <summary>GitHub Actions variables that hold host paths; they mean nothing inside the container.</summary>
    private static readonly HashSet<string> HostPathVariables = new(StringComparer.Ordinal)
    {
        "GITHUB_ACTION_PATH", "GITHUB_ENV", "GITHUB_EVENT_PATH", "GITHUB_OUTPUT", "GITHUB_PATH", "GITHUB_STATE",
        "GITHUB_STEP_SUMMARY", "GITHUB_WORKSPACE",
    };

    private static readonly string[] MapFiles = { ".build/.build.env.map", ".build/.app.env.map" };

    /// <summary>The release version stamped into this build of the tool, or null when it was built without one.</summary>
    public static string? ToolReleaseVersion(Assembly assembly) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildAgentReleaseVersion")?.Value is { Length: > 0 } version
            ? version
            : null;

    /// <summary>
    /// The image to run: <see cref="ImageOverrideVariable"/> when set, else the repository at the tool's own
    /// version. Never `latest` and never another version (I27).
    /// </summary>
    public static string ResolveImage(string? releaseVersion, Func<string, string?> getEnvironment)
    {
        if (getEnvironment(ImageOverrideVariable) is { Length: > 0 } image)
        {
            return image;
        }

        if (releaseVersion is null)
        {
            throw new LauncherException(new LauncherError(
                LauncherErrorCode.ImageUnavailable,
                $"This build of build-agent carries no release version, so it has no image of its own to run. " +
                $"Set {ImageOverrideVariable} to the image reference to use, or install a released BuildAgent.Tool."));
        }

        return $"{ImageRepository}:{releaseVersion}";
    }

    /// <summary>
    /// The DOCKER_HOST the build inside the container uses, and the host socket to mount for it. A host with no
    /// DOCKER_HOST, or a Windows named pipe the Linux container cannot open, gets the daemon's Unix socket, which
    /// Docker Engine and Docker Desktop both serve to containers.
    /// </summary>
    public static (string DockerHost, string? SocketPath) ResolveDockerHost(string? hostDockerHost)
    {
        var dockerHost = string.IsNullOrWhiteSpace(hostDockerHost) || hostDockerHost.StartsWith("npipe://", StringComparison.Ordinal)
            ? DefaultDockerHost
            : hostDockerHost;

        return dockerHost.StartsWith("unix://", StringComparison.Ordinal)
            ? (dockerHost, dockerHost.Substring("unix://".Length))
            : (dockerHost, null);
    }

    /// <summary>
    /// The host variables forwarded into the container, by name only: the parameter environment names, the
    /// variables the workspace's env map files read, and the CI context (`CI`, `GH_TOKEN`, `GITHUB_*` other than
    /// host paths). Only names set on the host are returned; their values never appear on a command line (I25).
    /// </summary>
    public static IReadOnlyList<string> ForwardedEnvironment(string workspace, IReadOnlyDictionary<string, string> hostEnvironment)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var name in ParameterEnvironmentNames)
        {
            names.Add(name);
        }

        foreach (var mapFile in MapFiles)
        {
            foreach (var name in MapFileSources(Path.Combine(workspace, mapFile)))
            {
                names.Add(name);
            }
        }

        foreach (var name in hostEnvironment.Keys)
        {
            if (name is "CI" or "GH_TOKEN" || (name.StartsWith("GITHUB_", StringComparison.Ordinal) && !HostPathVariables.Contains(name)))
            {
                names.Add(name);
            }
        }

        names.Remove("DOCKER_HOST");

        return names.Where(n => hostEnvironment.TryGetValue(n, out var value) && !string.IsNullOrEmpty(value)).ToList();
    }

    /// <summary>
    /// The `docker run` arguments: the workspace at /workspace, the socket when the Docker host is one, DOCKER_HOST,
    /// the forwarded names, then `build &lt;type&gt;` and the caller's arguments verbatim and in order.
    /// </summary>
    public static IReadOnlyList<string> DockerRunArguments(
        string image,
        string workspace,
        string type,
        IReadOnlyList<string> buildArguments,
        (string DockerHost, string? SocketPath) dockerHost,
        IReadOnlyList<string> forwardedEnvironment)
    {
        var arguments = new List<string> { "run", "--rm", "-v", $"{workspace}:/workspace" };

        if (dockerHost.SocketPath is { } socket)
        {
            arguments.AddRange(new[] { "-v", $"{socket}:{socket}" });
        }

        arguments.AddRange(new[] { "-w", "/workspace", "-e", $"DOCKER_HOST={dockerHost.DockerHost}" });

        foreach (var name in forwardedEnvironment)
        {
            arguments.AddRange(new[] { "-e", name });
        }

        arguments.Add(image);
        arguments.AddRange(new[] { "build", type });
        arguments.AddRange(buildArguments);

        return arguments;
    }

    /// <summary>The host variable each line of an env map file reads: `env:NAME` or a bare `NAME`; `const:` reads none.</summary>
    private static IEnumerable<string> MapFileSources(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var template = parts[1].Trim();
            if (template.StartsWith("const:", StringComparison.Ordinal))
            {
                continue;
            }

            var name = template.StartsWith("env:", StringComparison.Ordinal) ? template.Substring("env:".Length) : template;
            if (name.Length > 0)
            {
                yield return name;
            }
        }
    }
}
