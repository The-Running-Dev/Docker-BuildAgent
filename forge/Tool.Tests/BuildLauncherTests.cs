#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Launcher;
using Xunit;

namespace Tool.Tests;

/// <summary>
/// <see cref="BuildLauncher"/> against a scripted docker CLI: the exit status is the container's own (I28), a
/// failure before the build starts maps to the contract's launcher statuses, and no version is substituted (I27).
/// </summary>
public sealed class BuildLauncherTests
{
    private const string Image = "ghcr.io/the-running-dev/build-agent:2.1.0";
    private static readonly (string, string?) SocketHost = ("unix:///var/run/docker.sock", "/var/run/docker.sock");

    private readonly ScriptedDocker _docker = new();
    private readonly List<string> _output = new();
    private readonly List<string> _errors = new();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public async Task ReturnsTheContainersStatusUnchanged(int status)
    {
        _docker.RunStatus = status;

        Assert.Equal(status, await Launch());
        Assert.Equal(new[] { "image inspect " + Image }, _docker.Captured);
        Assert.Single(_docker.Runs);
    }

    [Fact]
    public async Task MissingWorkspace_Exits3WithoutTouchingDocker()
    {
        var status = await Launch(workspace: Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(3, status);
        Assert.Empty(_docker.Captured);
        Assert.Empty(_docker.Runs);
        Assert.StartsWith("WorkspaceInvalid:", Assert.Single(_errors));
    }

    [Fact]
    public async Task AbsentImage_IsPulledThenRun()
    {
        _docker.Results["image inspect " + Image] = (1, "No such image");

        Assert.Equal(0, await Launch());
        Assert.Equal(new[] { "image inspect " + Image, "pull " + Image }, _docker.Captured);
        Assert.Single(_docker.Runs);
    }

    [Fact]
    public async Task PullFailure_Exits5ImageUnavailableAndRunsNothing()
    {
        _docker.Results["image inspect " + Image] = (1, "No such image");
        _docker.Results["pull " + Image] = (1, "manifest unknown");

        Assert.Equal(5, await Launch());
        Assert.Empty(_docker.Runs);
        Assert.DoesNotContain(_docker.Captured, c => c.Contains("latest"));
        var error = Assert.Single(_errors);
        Assert.StartsWith("ImageUnavailable:", error);
        Assert.Contains(Image, error);
    }

    [Fact]
    public async Task UnreachableDaemon_Exits5DockerUnavailable()
    {
        _docker.Results["image inspect " + Image] = (1, "Cannot connect");
        _docker.Results["pull " + Image] = (1, "Cannot connect to the Docker daemon");
        _docker.Results["version --format {{.Server.Version}}"] = (1, "Cannot connect");

        Assert.Equal(5, await Launch());
        Assert.Empty(_docker.Runs);
        Assert.StartsWith("DockerUnavailable:", Assert.Single(_errors));
    }

    [Fact]
    public async Task NoDockerOnPath_Exits5DockerUnavailable()
    {
        _docker.ThrowNotFound = true;

        Assert.Equal(5, await Launch());
        Assert.StartsWith("DockerUnavailable:", Assert.Single(_errors));
    }

    [Fact]
    public async Task ContainerNotStarted_PassesTheStatusThroughAndNamesImageAndMount()
    {
        _docker.RunStatus = 125;

        Assert.Equal(125, await Launch());
        var error = Assert.Single(_errors);
        Assert.Contains(Image, error);
        Assert.Contains("mount", error);
    }

    [Fact]
    public async Task OutputNeverCarriesTheBuildArguments()
    {
        _docker.RunStatus = 2;

        await Launch(arguments: new[] { "--registry-token", "s3cr3t-value" });

        Assert.Contains(_docker.Runs.Single(), a => a == "s3cr3t-value");
        Assert.DoesNotContain(_output.Concat(_errors), line => line.Contains("s3cr3t-value"));
    }

    private Task<int> Launch(string? workspace = null, IReadOnlyList<string>? arguments = null)
    {
        var launcher = new BuildLauncher(_docker, _output.Add, _errors.Add);
        return launcher.RunAsync(
            Image,
            workspace ?? Path.GetTempPath(),
            "docker",
            arguments ?? Array.Empty<string>(),
            SocketHost,
            new[] { "RegistryToken" });
    }

    private sealed class ScriptedDocker : IDockerCli
    {
        public Dictionary<string, (int, string)> Results { get; } = new();

        public List<string> Captured { get; } = new();

        public List<IReadOnlyList<string>> Runs { get; } = new();

        public int RunStatus { get; set; }

        public bool ThrowNotFound { get; set; }

        public Task<(int ExitCode, string Output)> CaptureAsync(IReadOnlyList<string> arguments)
        {
            if (ThrowNotFound)
            {
                throw new Win32Exception(2, "An error occurred trying to start process 'docker'.");
            }

            var key = string.Join(" ", arguments);
            Captured.Add(key);
            return Task.FromResult(Results.TryGetValue(key, out var result) ? result : (0, string.Empty));
        }

        public Task<int> RunAttachedAsync(IReadOnlyList<string> arguments)
        {
            Runs.Add(arguments);
            return Task.FromResult(RunStatus);
        }
    }
}
