using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Xunit;

namespace Config.Tests;

/// <summary>
/// S13 — runs each build type's real entry point (the built DLL) against a temporary project root.
/// Removing the Config call from a build type's Main() makes the invalid-file cases below fail,
/// because that build would no longer exit 2 before running a target (S13.10).
/// </summary>
public sealed class BuildEntryPointTests : IDisposable
{
    private const string SecretValue = "s3cr3t-value-must-not-print";

    private readonly string _root;

    public BuildEntryPointTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "config-entry-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    public static IEnumerable<object[]> BuildTypes() => new[]
    {
        new object[] { "docker", "Docker" },
        new object[] { "node", "Node" },
        new object[] { "node-in-docker", "NodeInDocker" },
        new object[] { "forge", "Forge" },
    };

    private static string InvalidFile(string buildType) =>
        $"schemaVersion: 1\nbuildType: {buildType}\nparameters:\n" +
        "  bogus-key: x\n" +
        $"  notifications-web-hook-url: {SecretValue}\n" +
        "  dry-run: not-a-bool\n" +
        "  force-push: also-not-a-bool\n";

    private static string BuildTypeDll(string project)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = dir.Parent!.Name;
        var forgeDir = dir.Parent.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(forgeDir, project, "bin", configuration, project + ".dll");
        Assert.True(File.Exists(dll), $"Build type DLL not found: {dll}");

        return dll;
    }

    private (int ExitCode, string Output) Run(string project, string[]? arguments = null, Dictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(BuildTypeDll(project));
        info.ArgumentList.Add("--root");
        info.ArgumentList.Add(_root);
        foreach (var argument in arguments ?? Array.Empty<string>())
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["NUKE_TELEMETRY_OPTOUT"] = "1";
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            info.Environment[name] = value;
        }

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "build type did not exit");

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    private string EnvFilePath => Path.Combine(_root, ".build", ".build.env");

    // S13.1, S13.2, S13.3, S13.4, S13.8, S13.9 — an invalid file exits 2 before anything runs.
    [Theory]
    [MemberData(nameof(BuildTypes))]
    public void S13_1_InvalidFile_ExitsTwoBeforeAnyTarget(string buildType, string project)
    {
        var filePath = Path.Combine(_root, "buildagent.yml");
        var content = InvalidFile(buildType);
        File.WriteAllText(filePath, content);
        var mapPath = Path.Combine(_root, ".build", ".build.env.map");
        Directory.CreateDirectory(Path.GetDirectoryName(mapPath)!);
        File.WriteAllText(mapPath, "ImageTag=const:from-map\n");

        var (exitCode, output) = Run(project);

        Assert.Equal(2, exitCode);
        Assert.DoesNotContain("Executing target", output, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(EnvFilePath), "S13.2: no env file may be generated for a failed run");
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(content), File.ReadAllBytes(filePath));

        foreach (var key in new[] { "bogus-key", "notifications-web-hook-url", "dry-run", "force-push" })
        {
            Assert.Contains(key, output, StringComparison.Ordinal);
        }

        Assert.Contains("buildagent.yml", output, StringComparison.Ordinal);
        Assert.Contains("UnknownKey", output, StringComparison.Ordinal);
        Assert.Contains("SecretKeyRejected", output, StringComparison.Ordinal);
        Assert.Contains("ValueTypeMismatch", output, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretValue, output, StringComparison.Ordinal);
    }

    // S13.7 — a project with no file is not stopped by configuration.
    [Theory]
    [MemberData(nameof(BuildTypes))]
    public void S13_7_NoFile_IsNotRejectedByConfiguration(string buildType, string project)
    {
        _ = buildType;

        var (exitCode, output) = Run(project, new[] { "--help" });

        Assert.NotEqual(2, exitCode);
        Assert.DoesNotContain("Configuration is invalid", output, StringComparison.Ordinal);
    }

    private void WriteForgeFile(string parameters) =>
        File.WriteAllLines(
            Path.Combine(_root, "buildagent.yml"),
            new[] { "schemaVersion: 1", "buildType: forge", "parameters:", parameters });

    // S13.5 — a file-only value is the effective build parameter.
    [Fact]
    public void S13_5_FileValueReachesTheBuildParameter()
    {
        WriteForgeFile("  dry-run: true");

        var (_, output) = Run("Forge");

        Assert.Contains("[CONFIG] DryRun: True", output, StringComparison.Ordinal);
    }

    // S13.6 — an invocation argument still wins over the file.
    [Fact]
    public void S13_6_ArgumentBeatsFileValue()
    {
        WriteForgeFile("  dry-run: true");

        var (_, output) = Run("Forge", new[] { "--dry-run", "false" });

        Assert.Contains("[CONFIG] DryRun: False", output, StringComparison.Ordinal);
    }

    // S13.6 — the process environment still wins over the file.
    [Fact]
    public void S13_6_ProcessEnvironmentBeatsFileValue()
    {
        WriteForgeFile("  dry-run: true");

        var (_, output) = Run("Forge", environment: new() { ["DryRun"] = "false" });

        Assert.Contains("[CONFIG] DryRun: False", output, StringComparison.Ordinal);
    }

    // S13.7 — no file: the parameter keeps the value it had before S13.
    [Fact]
    public void S13_7_NoFile_ParameterKeepsItsDefault()
    {
        var (_, output) = Run("Forge");

        Assert.Contains("[CONFIG] DryRun: False", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Configuration error", output, StringComparison.Ordinal);
    }

    // S13.9 — a run that passes validation leaves the project file's bytes untouched.
    [Fact]
    public void S13_9_ValidFile_BytesUnchangedAfterRun()
    {
        WriteForgeFile("  dry-run: true");
        var filePath = Path.Combine(_root, "buildagent.yml");
        var before = File.ReadAllBytes(filePath);

        var (_, output) = Run("Forge");

        Assert.DoesNotContain("Configuration error", output, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(filePath));
    }

    // S13.4 — exit 2 reaches the caller of `build <type>` unchanged through the wrapper script.
    [Theory]
    [MemberData(nameof(BuildTypes))]
    public void S13_4_WrapperPassesExitTwoThrough(string buildType, string project)
    {
        File.WriteAllText(Path.Combine(_root, "buildagent.yml"), InvalidFile(buildType));

        var dll = BuildTypeDll(project);
        var repoRoot = new DirectoryInfo(Path.GetDirectoryName(dll)!).Parent!.Parent!.Parent!.Parent!.FullName;
        var script = Path.Combine(repoRoot, "scripts", "nuke", "build.ps1");
        Assert.True(File.Exists(script), $"Wrapper script not found: {script}");

        var info = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
                 {
                     "-NoProfile", "-NonInteractive", "-File", script, buildType,
                     "-workingDir", _root, "-artifactsDir", Path.GetDirectoryName(dll)!,
                 })
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["NUKE_TELEMETRY_OPTOUT"] = "1";

        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(120_000), "wrapper did not exit");

        Assert.True(process.ExitCode == 2, $"expected exit 2, got {process.ExitCode}: {stdout.Result}{stderr.Result}");
    }
}
