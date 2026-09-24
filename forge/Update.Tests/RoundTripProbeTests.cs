#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Update.Tests;

/// <summary>
/// S2.22: runs the round-trip probe `## Unresolved` U-10 requires against a real Docker daemon. Each shape
/// below is created directly (bypassing the Updater, to stand in for "a container someone already has
/// running"), inspected and recreated through the exact <see cref="CliDockerRuntime"/> mapping and
/// <see cref="Updater.BuildReplacementSpec"/> logic the Updater itself uses, and the recreated container is
/// inspected again to check the shape's defining property survived.
///
/// This closes the gap <see cref="CliDockerRuntime"/> disclosed at S2.22/S2.23 ("written against documented
/// `docker inspect` JSON shapes, not verified against a live daemon"): every shape here is exercised against a
/// real daemon in CI (ubuntu-latest, Docker set up by `.github/actions/common`), which is the Linux verification
/// S2.23 requires. A machine with no `docker` on PATH or no reachable daemon — including this repository's own
/// Windows dev checkouts — skips by returning early rather than failing; that is the stated, unverified gap
/// S2.23 names for Windows, not a defect here.
///
/// Per S2.22, a shape found not to round-trip is recorded here (as a failing assertion naming what differed),
/// not folded into `Updater.RefuseUnsupportedShape`'s refusal list — widening that list is a separate,
/// deliberate decision this slice does not make on the strength of this probe alone.
/// </summary>
public sealed class RoundTripProbeTests : IAsyncLifetime
{
    private const string Image = "alpine:3.20";
    private readonly string _prefix = "buildagent-probe-" + Guid.NewGuid().ToString("N")[..8];
    private readonly CliDockerRuntime _runtime = new();
    private static readonly Lazy<bool> DaemonAvailableLazy = new(ProbeDaemon);

    private static bool DaemonAvailable => DaemonAvailableLazy.Value;

    public async Task InitializeAsync()
    {
        if (!DaemonAvailable)
        {
            return;
        }

        await Run("pull", Image);
    }

    public async Task DisposeAsync()
    {
        if (!DaemonAvailable)
        {
            return;
        }

        // Best-effort: remove everything this run's prefix could have created, whether or not the test that
        // created it passed.
        await Run("rm", "--force", $"{_prefix}-src");
        await Run("rm", "--force", $"{_prefix}-dst");
        await Run("volume", "rm", "--force", $"{_prefix}-vol");
        await Run("network", "rm", $"{_prefix}-net");
    }

    [Fact]
    public async Task BindMount_RoundTrips()
    {
        if (!DaemonAvailable) return;

        var hostPath = System.IO.Path.GetTempPath().TrimEnd('\\', '/');
        await CreateSource("--mount", $"type=bind,source={hostPath},destination=/probe-bind,readonly");

        var replacement = await RoundTrip();

        var mount = Assert.Single(replacement.Mounts, m => m.Destination == "/probe-bind");
        Assert.Equal(MountKind.Bind, mount.Kind);
        Assert.True(mount.ReadOnly);
        Assert.False(mount.IsAnonymousVolume);
    }

    [Fact]
    public async Task NamedVolume_RoundTrips()
    {
        if (!DaemonAvailable) return;

        var volumeName = $"{_prefix}-vol";
        await Run("volume", "create", volumeName);
        await CreateSource("--mount", $"type=volume,source={volumeName},destination=/probe-vol");

        var replacement = await RoundTrip();

        var mount = Assert.Single(replacement.Mounts, m => m.Destination == "/probe-vol");
        Assert.Equal(MountKind.Volume, mount.Kind);
        Assert.Equal(volumeName, mount.Source);
        Assert.False(mount.IsAnonymousVolume);
    }

    [Fact]
    public async Task AnonymousVolume_IsDetectedAsAnonymous()
    {
        if (!DaemonAvailable) return;

        // The known-unsupported control case (S2.8): confirms the heuristic CliDockerRuntime uses to tell an
        // anonymous volume from a named one actually matches what a live daemon reports, rather than only the
        // documented shape it was written against.
        await CreateSource("--mount", "type=volume,destination=/probe-anon");

        var source = await _runtime.InspectAsync($"{_prefix}-src");
        var mount = Assert.Single(source!.Mounts, m => m.Destination == "/probe-anon");
        Assert.True(mount.IsAnonymousVolume);
    }

    [Fact]
    public async Task Tmpfs_RoundTrips()
    {
        if (!DaemonAvailable) return;

        await CreateSource("--mount", "type=tmpfs,destination=/probe-tmpfs");

        var replacement = await RoundTrip();

        var mount = Assert.Single(replacement.Mounts, m => m.Destination == "/probe-tmpfs");
        Assert.Equal(MountKind.Tmpfs, mount.Kind);
    }

    [Fact]
    public async Task PublishedPort_RoundTrips()
    {
        if (!DaemonAvailable) return;

        await CreateSource("--publish", "0.0.0.0:0:8080/tcp");

        var replacement = await RoundTrip();

        var port = Assert.Single(replacement.Ports, p => p.ContainerPort == 8080);
        Assert.Equal("tcp", port.Protocol);
        Assert.NotNull(port.HostPort);
    }

    [Fact]
    public async Task ExposedOnlyPort_RoundTrips()
    {
        if (!DaemonAvailable) return;

        await CreateSource("--expose", "9090/tcp");

        var replacement = await RoundTrip();

        var port = Assert.Single(replacement.Ports, p => p.ContainerPort == 9090);
        Assert.Null(port.HostPort);
    }

    [Fact]
    public async Task RestartPolicyWithRetryCount_RoundTrips()
    {
        if (!DaemonAvailable) return;

        await CreateSource("--restart", "on-failure:3");

        var replacement = await RoundTrip();

        Assert.Equal("on-failure:3", replacement.RestartPolicy);
    }

    [Fact]
    public async Task EnvCommandAndLabels_RoundTrip()
    {
        if (!DaemonAvailable) return;

        await CreateSource(
            "--env", "PROBE_VAR=probe-value",
            "--label", "com.example.probe=probe-label");

        var replacement = await RoundTrip("sh", "-c", "sleep 1");

        Assert.Contains("PROBE_VAR=probe-value", replacement.Env);
        Assert.Equal("probe-label", replacement.Labels["com.example.probe"]);
    }

    [Fact]
    public async Task SecondNetwork_RoundTrips()
    {
        if (!DaemonAvailable) return;

        var networkName = $"{_prefix}-net";
        await Run("network", "create", networkName);
        await CreateSource();
        await Run("network", "connect", networkName, $"{_prefix}-src");

        var replacement = await RoundTrip();

        Assert.Contains(networkName, replacement.Networks);
    }

    private async Task CreateSource(params string[] extraArgs)
    {
        var args = new[] { "create", "--name", $"{_prefix}-src" }
            .Concat(extraArgs)
            .Append(Image)
            .ToArray();
        await Run(args);
    }

    /// <summary>Inspects the source this test created, builds a replacement spec through the Updater's own
    /// logic, creates the replacement, and inspects it back — the exact sequence <c>Updater.RunAsync</c> runs
    /// between "stop the target" and "start the replacement".</summary>
    private async Task<ContainerInspection> RoundTrip(params string[] replacementCommand)
    {
        var source = await _runtime.InspectAsync($"{_prefix}-src")
            ?? throw new InvalidOperationException("Probe setup did not create the source container.");

        var spec = Updater.BuildReplacementSpec($"{_prefix}-dst", source, source.ImageId);
        if (replacementCommand.Length > 0)
        {
            spec = spec with { Command = replacementCommand };
        }

        await _runtime.CreateReplacementAsync(spec);
        return await _runtime.InspectAsync($"{_prefix}-dst")
            ?? throw new InvalidOperationException("Round-trip create did not produce an inspectable container.");
    }

    private static bool ProbeDaemon()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "version --format {{.Server.Version}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process == null)
            {
                return false;
            }

            return process.WaitForExit(5000) && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> Run(params string[] args)
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
