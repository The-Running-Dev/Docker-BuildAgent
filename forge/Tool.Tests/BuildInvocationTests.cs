#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Launcher;
using Xunit;

namespace Tool.Tests;

/// <summary>
/// The host-to-image translation of `build-agent build` (contract § Global tool): image pinning (I27), the accepted
/// types (I29), Docker host resolution, environment passthrough by name (I25) and argument order. None of it needs
/// a daemon, so it runs on Windows as well as Linux.
/// </summary>
public sealed class BuildInvocationTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "build-agent-tool-" + Guid.NewGuid().ToString("N"));

    public BuildInvocationTests() => Directory.CreateDirectory(_workspace);

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public void AcceptedBuildTypes_AreExactlyTheInImageCommandsSet()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "scripts", "nuke", "build.ps1"));
        var validateSet = Regex.Match(script, @"\[ValidateSet\(([^)]*)\)\]\s*\[string\]\$type");

        Assert.True(validateSet.Success, "build.ps1 no longer declares the type's ValidateSet.");
        var inImage = Regex.Matches(validateSet.Groups[1].Value, "'([^']+)'").Select(m => m.Groups[1].Value);
        Assert.Equal(inImage.OrderBy(t => t), BuildInvocation.AcceptedBuildTypes.OrderBy(t => t));
    }

    [Fact]
    public void ParameterEnvironmentNames_AreExactlyThePublishedOnes()
    {
        var published = File.ReadAllLines(Path.Combine(RepositoryRoot(), "documentation", "docs", "parameters.md"))
            .Select(line => line.Split('|'))
            .Where(cells => cells.Length > 3 && (cells[1].Trim().StartsWith("`--") || cells[1].Trim() == "none"))
            .Select(cells => cells[2].Trim().Trim('`'))
            .Where(name => name != "none")
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(published, BuildInvocation.ParameterEnvironmentNames.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ResolveImage_PinsToTheToolsOwnVersion()
    {
        Assert.Equal("ghcr.io/the-running-dev/build-agent:2.1.0", BuildInvocation.ResolveImage("2.1.0", _ => null));
    }

    [Fact]
    public void ResolveImage_OverrideReplacesTheImage()
    {
        var image = BuildInvocation.ResolveImage("2.1.0", name => name == "BUILD_AGENT_IMAGE" ? "example.com/agent:dev" : null);

        Assert.Equal("example.com/agent:dev", image);
    }

    [Fact]
    public void ResolveImage_WithoutAVersionOrOverride_FailsRatherThanChoosingOne()
    {
        var ex = Assert.Throws<LauncherException>(() => BuildInvocation.ResolveImage(null, _ => null));

        Assert.Equal(LauncherErrorCode.ImageUnavailable, ex.Error.Code);
        Assert.Equal(5, ex.ExitStatus);
        Assert.DoesNotContain("latest", ex.Message);
    }

    [Theory]
    [InlineData(null, "unix:///var/run/docker.sock", "/var/run/docker.sock")]
    [InlineData("", "unix:///var/run/docker.sock", "/var/run/docker.sock")]
    [InlineData("npipe:////./pipe/docker_engine", "unix:///var/run/docker.sock", "/var/run/docker.sock")]
    [InlineData("unix:///run/user/1000/docker.sock", "unix:///run/user/1000/docker.sock", "/run/user/1000/docker.sock")]
    [InlineData("tcp://build-host:2375", "tcp://build-host:2375", null)]
    public void ResolveDockerHost_GivesTheContainerAHostItCanReach(string? host, string expectedHost, string? expectedSocket)
    {
        var (dockerHost, socket) = BuildInvocation.ResolveDockerHost(host);

        Assert.Equal(expectedHost, dockerHost);
        Assert.Equal(expectedSocket, socket);
    }

    [Fact]
    public void ForwardedEnvironment_ForwardsParametersMapSourcesAndCiContextOnlyWhenSet()
    {
        Directory.CreateDirectory(Path.Combine(_workspace, ".build"));
        File.WriteAllLines(Path.Combine(_workspace, ".build", ".build.env.map"), new[]
        {
            "# comment",
            "RegistryUser=env:MY_REGISTRY_USER",
            "ApiKey=APP_API_KEY",
            "ImageTag=const:my-app",
            "Unset=env:NOT_SET_ANYWHERE",
        });
        File.WriteAllLines(Path.Combine(_workspace, ".build", ".app.env.map"), new[] { "Port=APP_PORT" });

        var host = new Dictionary<string, string>
        {
            ["RegistryToken"] = "secret",
            ["DryRun"] = "true",
            ["ImageTag"] = string.Empty,
            ["MY_REGISTRY_USER"] = "me",
            ["APP_API_KEY"] = "key",
            ["APP_PORT"] = "8080",
            ["CI"] = "true",
            ["GITHUB_TOKEN"] = "token",
            ["GITHUB_REF"] = "refs/heads/main",
            ["GH_TOKEN"] = "token",
            ["GITHUB_OUTPUT"] = "/home/runner/work/_temp/output",
            ["GITHUB_WORKSPACE"] = "/home/runner/work/repo",
            ["DOCKER_HOST"] = "unix:///var/run/docker.sock",
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/home/runner",
        };

        var forwarded = BuildInvocation.ForwardedEnvironment(_workspace, host);

        Assert.Equal(
            new[] { "APP_API_KEY", "APP_PORT", "CI", "DryRun", "GH_TOKEN", "GITHUB_REF", "GITHUB_TOKEN", "MY_REGISTRY_USER", "RegistryToken" },
            forwarded);
    }

    [Fact]
    public void DockerRunArguments_MountTheWorkspaceAndPassArgumentsVerbatimAndInOrder()
    {
        var arguments = BuildInvocation.DockerRunArguments(
            "ghcr.io/the-running-dev/build-agent:2.1.0",
            "/src/app",
            "docker",
            new[] { "--image-tag", "my app", "--dry-run", "true", "--image-tag", "second" },
            ("unix:///var/run/docker.sock", "/var/run/docker.sock"),
            new[] { "RegistryToken" });

        Assert.Equal(
            new[]
            {
                "run", "--rm", "-v", "/src/app:/workspace", "-v", "/var/run/docker.sock:/var/run/docker.sock",
                "-w", "/workspace", "-e", "DOCKER_HOST=unix:///var/run/docker.sock", "-e", "RegistryToken",
                "ghcr.io/the-running-dev/build-agent:2.1.0", "build", "docker",
                "--image-tag", "my app", "--dry-run", "true", "--image-tag", "second",
            },
            arguments);
    }

    [Fact]
    public void DockerRunArguments_TcpHostMountsNoSocket()
    {
        var arguments = BuildInvocation.DockerRunArguments(
            "img:1", @"C:\src\app", "node", Array.Empty<string>(), ("tcp://build-host:2375", null), Array.Empty<string>());

        Assert.Equal(
            new[] { "run", "--rm", "-v", @"C:\src\app:/workspace", "-w", "/workspace", "-e", "DOCKER_HOST=tcp://build-host:2375", "img:1", "build", "node" },
            arguments);
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "forge", "Forge.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root (a directory holding forge/Forge.sln).");
    }
}
