using System.IO;
using System.Linq;
using System.Text.Json;

using DotNetEnv;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.Git;

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

    string ResolvedVersion => VersionFile.Exists() ? VersionFile.ReadAllText().Trim() : null;

    public static int Main()
    {
        var configFile = RootDirectory / ".nuke/config";
        var envFile = RootDirectory / ".env";

        if (File.Exists(configFile))
        {
            Env.Load(configFile);

            Log.Information("✅ Loaded Configuration from .nuke/config");
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
            Log.Information("ℹ️ No .env File Found");
        }

        return Execute<Build>(x => x.BuildAndPush);
    }

    Target BuildAndPush => _ => _
        .DependsOn(PrintInfo, BuildContainer)
        .Executes(() =>
        {
            Log.Information("✅ Build Step Complete");

            if (!IsLocalBuild && !DryRun)
            {
                Log.Information("🚀 CI Build Detected — Continuing to Push and Tag...");
                
                Push.Invoke(null);
                
                Tag.Invoke(null);
            }
            else
            {
                Log.Information($"🏃 Skipping Push/Tag (IsLocalBuild: {IsLocalBuild}, DryRun: {DryRun})");
            }
        });

    Target Tag => _ => _
        .OnlyWhenStatic(() => !IsLocalBuild && !DryRun)
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(ResolvedVersion))
                Assert.Fail(".resolved-version is Missing.");

            var existingTags = GitTasks.Git("tag");

            if (existingTags.All(l => l.Text != ResolvedVersion))
            {
                GitTasks.Git($"tag {ResolvedVersion}");
                GitTasks.Git($"push origin {ResolvedVersion}");

                Log.Information($"🏷️ Created and Pushed Tag: {ResolvedVersion}");
            }
            else
            {
                Log.Information($"✅ Tag {ResolvedVersion} Already Exists");
            }
        });

    Target Push => _ => _
        .OnlyWhenStatic(() => !IsLocalBuild && !DryRun)
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(GitHubPackagesToken))
                Assert.Fail("❌ GitHubPackagesToken is Not Set.");

            DockerTasks.DockerLogin(s => s
                .SetServer("ghcr.io")
                .SetUsername(GitHubUsername)
                .SetPassword(GitHubPackagesToken));

            DockerTasks.DockerPush(s => s
                .SetName($"{Repository}/{ImageTag}:{ResolvedVersion}"));

            DockerTasks.DockerPush(s => s
                .SetName($"{Repository}/{ImageTag}:latest"));

            Log.Information($"📤 Pushed Docker Images: {ResolvedVersion}, latest");
        });

    Target BuildContainer => _ => _
        .DependsOn(GetVersion, ValidateInputs)
        .Executes(() =>
        {
            DockerTasks.DockerBuild(s => s
                .SetPath(RootDirectory)
                .SetFile(RootDirectory / Dockerfile)
                .SetTag($"{Repository}/{ImageTag}:latest"));

            DockerTasks.DockerTag(s => s
                .SetSourceImage($"{Repository}/{ImageTag}:latest")
                .SetTargetImage($"{Repository}/{ImageTag}:{ResolvedVersion}"));

            Log.Information($"🐳 Built and Tagged Images: {ResolvedVersion}, latest");
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
                Assert.Fail("❌ Failed to extract SemVer from GitVersion.");

            VersionFile.WriteAllText(GitVersion.SemVer);

            Log.Information($"🔖 GitVersion Resolved via CLI: {GitVersion.SemVer}");
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

            if (string.IsNullOrWhiteSpace(ResolvedVersion))
                Assert.Fail("❌ .resolved-version is Missing. Run GetVersion First.");
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

class GitVersionInfo
{
    public string SemVer { get; set; }
}