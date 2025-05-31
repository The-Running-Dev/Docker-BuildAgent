using System.IO;
using System.Linq;
using System.Text.Json;

using DotNetEnv;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.Docker;

using Serilog;

class Build : NukeBuild
{
    [Parameter("GitHub Token for Pushing Images")]
    [Secret]
    readonly string GitHubPackagesToken;

    [Parameter("Docker Image Tag")]
    readonly string ImageTag;

    [Parameter("Docker Image Repository")]
    readonly string Repository;

    [Parameter("GitHub Username")]
    readonly string GitHubUsername;

    [Parameter("Dockerfile Path")]
    readonly string Dockerfile = "Dockerfile";

    [Parameter("Enable Dry Run (Skip Push and Tag)")]
    readonly bool DryRun;

    GitVersionInfo GitVersion;

    AbsolutePath VersionFile => RootDirectory / ".resolved-version";

    [Parameter("Force Push/Tag Even During Local Builds")]
    readonly bool ForceCiBehavior;
    

    string GetResolvedVersion()
    {
        if (!VersionFile.Exists())
        {
            Assert.Fail("❌ .resolved-version is Missing. Run GetVersion First.");
        }

        return VersionFile.ReadAllText().Trim();
    }

    public static int Main()
    {
        var configFile = RootDirectory / ".nuke/config";
        var envFile = RootDirectory / ".env";

        if (File.Exists(configFile))
        {
            Env.Load(configFile);
            Log.Information("✅ Loaded Config from .nuke/config");
        }
        else
        {
            Log.Information("ℹ️ No .nuke/config Found");
        }

        if (File.Exists(envFile))
        {
            Env.Load(envFile);
            Log.Information("✅ Loaded Secrets from .env");
        }
        else
        {
            Log.Information("ℹ️ No .env Found");
        }

        return Execute<Build>(x => x.BuildAndPush);
    }

    Target BuildAndPush => _ => _
        .DependsOn(GetVersion, PrintInfo, BuildContainer, Push, Tag)
        .Executes(() =>
        {
            Log.Information("✅ Build Step Complete");

            if (!IsLocalBuild && !DryRun)
            {
                Log.Information("🚀 CI Build Detected — Pushing and Tagging...");
                
                Push.Invoke(null);
                
                Tag.Invoke(null);
            }
            else
            {
                Log.Information($"🏃 Skipping Push/Tag (IsLocalBuild: {IsLocalBuild}, DryRun: {DryRun})");
            }
        });

    Target GetVersion => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            var process = ProcessTasks.StartProcess("dotnet-gitversion", "/output json", RootDirectory, logOutput: true);
            process.AssertZeroExitCode();

            var output = process.Output.StdToText();
            GitVersion = JsonSerializer.Deserialize<GitVersionInfo>(output);

            if (string.IsNullOrWhiteSpace(GitVersion?.SemVer))
                Assert.Fail("❌ Failed to Extract SemVer from GitVersion.");

            VersionFile.WriteAllText(GitVersion.SemVer);
            Log.Information($"🔖 GitVersion Resolved: {GitVersion.SemVer}");
        });

    Target Clean => _ => _
        .Executes(() =>
        {
            if (VersionFile.Exists())
            {
                VersionFile.DeleteFile();
                Log.Information("🧹 Removed .resolved-version");
            }
        });

    Target ValidateInputs => _ => _
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(ImageTag))
                Assert.Fail("❌ ImageTag is Required.");

            GetResolvedVersion(); // Will assert if missing
        });

    Target BuildContainer => _ => _
        .DependsOn(GetVersion, ValidateInputs)
        .Executes(() =>
        {
            var version = GetResolvedVersion();

            DockerTasks.DockerBuild(s => s
                .SetPath(RootDirectory)
                .SetFile(RootDirectory / Dockerfile)
                .SetTag($"{Repository}/{ImageTag}:latest"));

            DockerTasks.DockerTag(s => s
                .SetSourceImage($"{Repository}/{ImageTag}:latest")
                .SetTargetImage($"{Repository}/{ImageTag}:{version}"));

            Log.Information($"🐳 Built and Tagged: {version}, latest");
        });

    Target Push => _ => _
        .DependsOn(GetVersion)
        .OnlyWhenDynamic(() => ForceCiBehavior || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(GitHubPackagesToken))
            {
                Assert.Fail("❌ GitHubPackagesToken is not Set.");
            }

            var version = GetResolvedVersion();

            DockerTasks.DockerLogin(s => s
                .SetServer("ghcr.io")
                .SetUsername(GitHubUsername)
                .SetPassword(GitHubPackagesToken));

            DockerTasks.DockerPush(s => s
                .SetName($"{Repository}/{ImageTag}:{version}"));

            DockerTasks.DockerPush(s => s
                .SetName($"{Repository}/{ImageTag}:latest"));

            Log.Information($"📤 Pushed Docker Images: {version}, latest");
        });

    Target Tag => _ => _
        .DependsOn(GetVersion)
        .OnlyWhenDynamic(() => ForceCiBehavior || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            var version = GetResolvedVersion();
            var existingTags = GitTasks.Git("tag");

            if (existingTags.All(l => l.Text != version))
            {
                GitTasks.Git($"tag {version}");
                GitTasks.Git($"push origin {version}");
                Log.Information($"🏷️ Created and Pushed Tag: {version}");
            }
            else
            {
                Log.Information($"✅ Tag {version} Already Exists");
            }
        });

    Target PrintInfo => _ => _
        .Executes(() =>
        {
            Log.Information($"🔧 Image Tag: {ImageTag}");
            Log.Information($"🔧 Repository: {Repository}");
            Log.Information($"🔧 GitHub Username: {GitHubUsername}");
            Log.Information($"🔧 Git Version: {GitVersion?.SemVer ?? "Unavailable"}");
            Log.Information($"🔧 Dry Run: {DryRun}");
        });
}

record GitVersionInfo
{
    public string SemVer { get; init; }
}
