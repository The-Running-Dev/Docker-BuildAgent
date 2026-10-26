#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Launcher;

/// <summary>Runs the `docker` CLI; arguments go element by element, never through a shell string.</summary>
public interface IDockerCli
{
    /// <summary>Runs docker with its output captured. Throws <see cref="Win32Exception"/> when docker is not on PATH.</summary>
    Task<(int ExitCode, string Output)> CaptureAsync(IReadOnlyList<string> arguments);

    /// <summary>Runs docker attached to this process's console, so the build's output streams as it happens.</summary>
    Task<int> RunAttachedAsync(IReadOnlyList<string> arguments);
}

/// <summary>Status docker run itself exits with when the daemon refuses to create or start the container.</summary>
public static class DockerRunStatus
{
    public const int ContainerNotStarted = 125;
}

public sealed class BuildLauncher
{
    private readonly IDockerCli _docker;
    private readonly Action<string> _writeLine;
    private readonly Action<string> _writeError;

    public BuildLauncher(IDockerCli docker, Action<string> writeLine, Action<string> writeError)
    {
        _docker = docker;
        _writeLine = writeLine;
        _writeError = writeError;
    }

    /// <summary>
    /// Runs one build and returns the status to exit with: the container's own, unchanged (I28), or the
    /// launcher's status for a failure before the build starts (contract § Launchers).
    /// </summary>
    public async Task<int> RunAsync(
        string image,
        string workspace,
        string type,
        IReadOnlyList<string> buildArguments,
        (string DockerHost, string? SocketPath) dockerHost,
        IReadOnlyList<string> forwardedEnvironment)
    {
        try
        {
            if (!Directory.Exists(workspace))
            {
                throw new LauncherException(new LauncherError(
                    LauncherErrorCode.WorkspaceInvalid,
                    $"Workspace path '{workspace}' does not exist or is not a directory."));
            }

            await EnsureImageAsync(image);

            var arguments = BuildInvocation.DockerRunArguments(image, workspace, type, buildArguments, dockerHost, forwardedEnvironment);
            _writeLine($"Executing: docker run ... {image} build {type} [arguments hidden for security]");

            int status;
            try
            {
                status = await _docker.RunAttachedAsync(arguments);
            }
            catch (Win32Exception ex)
            {
                throw DockerUnavailable(ex.Message);
            }

            if (status == DockerRunStatus.ContainerNotStarted)
            {
                // The build never ran: docker refused the container, most often the workspace or socket mount
                // (Docker Desktop file sharing). The status still passes through unchanged.
                _writeError(
                    $"Docker could not start a container from '{image}' (status {status}). The image is present, so the " +
                    $"mount of '{workspace}'{(dockerHost.SocketPath is { } socket ? $" or of the socket '{socket}'" : string.Empty)} " +
                    "was most likely refused; docker's message above names the cause.");
            }
            else if (status != 0)
            {
                _writeError(new LauncherError(LauncherErrorCode.BuildFailed, $"The build in '{image}' exited with status {status}.").Message);
            }

            return status;
        }
        catch (LauncherException ex)
        {
            _writeError($"{ex.Error.Code}: {ex.Message}");
            return ex.ExitStatus;
        }
    }

    /// <summary>The image is present or pulled; anything else fails without substituting a version (I27).</summary>
    private async Task EnsureImageAsync(string image)
    {
        try
        {
            if ((await _docker.CaptureAsync(new[] { "image", "inspect", image })).ExitCode == 0)
            {
                return;
            }

            _writeLine($"Pulling {image}...");
            var pull = await _docker.CaptureAsync(new[] { "pull", image });
            if (pull.ExitCode == 0)
            {
                return;
            }

            if ((await _docker.CaptureAsync(new[] { "version", "--format", "{{.Server.Version}}" })).ExitCode != 0)
            {
                throw DockerUnavailable(pull.Output.Trim());
            }

            throw new LauncherException(new LauncherError(
                LauncherErrorCode.ImageUnavailable,
                $"Image '{image}' could not be pulled: {pull.Output.Trim().TrimEnd('.')}. No other version is used in its place."));
        }
        catch (Win32Exception ex)
        {
            throw DockerUnavailable(ex.Message);
        }
    }

    private static LauncherException DockerUnavailable(string detail) =>
        new(new LauncherError(LauncherErrorCode.DockerUnavailable, $"Docker is not available: {detail}"));
}

public sealed class DockerCli : IDockerCli
{
    public async Task<(int ExitCode, string Output)> CaptureAsync(IReadOnlyList<string> arguments)
    {
        using var process = Start(arguments, redirect: true);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, string.Join(" ", new[] { await stdout, await stderr }.Where(s => s.Length > 0)));
    }

    public async Task<int> RunAttachedAsync(IReadOnlyList<string> arguments)
    {
        using var process = Start(arguments, redirect: false);
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private static Process Start(IReadOnlyList<string> arguments, bool redirect)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ?? throw new Win32Exception("docker could not be started.");
    }
}
