using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using DotNetEnv;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.Docker;

using Serilog;

class Docker : NukeBuild
{
    [Parameter("Docker Image Repository")]
    readonly string Repository;

    [Parameter("Repository Username")]
    readonly string RepositoryUsername;

    [Parameter("Repository Token for Pushing Images")]
    [Secret]
    readonly string RepositoryToken;

    [Parameter("Docker Image Tag")]
    readonly string ImageTag;
    
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
            Assert.Fail($"❌ {VersionFile} is Missing. Run GetVersion First.");
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

            Log.Information($"✅ Loaded Config from {configFile}");
        }


        if (File.Exists(envFile))
        {
            Env.Load(envFile);

            Log.Information($"✅ Loaded Secrets from {envFile}");
        }

        ProcessTasks.StartProcess("git", $"config --global --add safe.directory \"{RootDirectory}\"").AssertZeroExitCode();

        return Execute<Docker>(x => x.ContainerCI);
    }

    Target ContainerCI => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            Log.Information("✅ Container CI Complete");
        });

    Target GetVersion => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            var process = ProcessTasks.StartProcess("dotnet-gitversion", "/output json", RootDirectory, logOutput: false);
            process.AssertZeroExitCode();

            var output = process.Output.StdToText();
            GitVersion = JsonSerializer.Deserialize<GitVersionInfo>(output);

            if (string.IsNullOrWhiteSpace(GitVersion?.SemVer))
            {
                Assert.Fail("❌ Failed to Get SemVer from GitVersion.");
            }

            VersionFile.WriteAllText(GitVersion.SemVer);

            Log.Information($"🔖 GitVersion: {GitVersion.SemVer}");
        });

    Target Clean => _ => _
        .Executes(() =>
        {
            if (VersionFile.Exists())
            {
                VersionFile.DeleteFile();

                Log.Information($"🧹 Removed {VersionFile}");
            }
        });

    Target ValidateInputs => _ => _
        .DependsOn(GetVersion)
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(ImageTag))
            {
                Assert.Fail("❌ ImageTag is Required.");
            }

            GetResolvedVersion();
        });

    Target BuildContainer => _ => _
        .DependsOn(PrintInfo)
        .Executes(() =>
        {
            var version = GetResolvedVersion();

            Log.Information($"Building: {RootDirectory/Dockerfile}, Tag: {Repository}/{ImageTag}:latest...");

            DockerTasks.DockerBuild(s => s
                .SetProcessLogger((_, _) => { })
                .SetPath(RootDirectory)
                .SetFile(RootDirectory / Dockerfile)
                .SetTag($"{Repository}/{ImageTag}:latest"));
            
            DockerTasks.DockerTag(s => s
                .SetSourceImage($"{Repository}/{ImageTag}:latest")
                .SetTargetImage($"{Repository}/{ImageTag}:{version}"));

            Log.Information($"🐳 Built and Tagged: {version}, latest");
        });

    Target Publish => _ => _
        .DependsOn(Push)
        .Executes(() =>
        {
            Log.Information("✅ Publish Step Complete");
        });

    Target Push => _ => _
        .DependsOn(Tag)
        .OnlyWhenDynamic(() => ForceCiBehavior || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(RepositoryToken))
            {
                Assert.Fail("❌ Repository Token is Not Set.");
            }

            var version = GetResolvedVersion();

            DockerTasks.DockerLogin(s => s
                .SetServer(Regex.Replace(Repository, @"/.*$", ""))
                .SetUsername(RepositoryUsername)
                .SetPassword(RepositoryToken));

            DockerTasks.DockerPush(s => s.SetName($"{Repository}/{ImageTag}:{version}"));
            DockerTasks.DockerPush(s => s.SetName($"{Repository}/{ImageTag}:latest"));

            Log.Information($"📤 Pushed Docker Images: {version}, latest");
        });

    Target Tag => _ => _
        .DependsOn(BuildContainer)
        .OnlyWhenDynamic(() => ForceCiBehavior || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            var version = GetResolvedVersion();
            var existingTags = GitTasks.Git("tag");

            if (existingTags.All(l => l.Text != $"v{version}"))
            {
                GitTasks.Git($"tag v{version}");

                GitTasks.Git($"push origin {version}");
                
                Log.Information($"🏷️ Created and Pushed Tag: {version}");
            }
            else
            {
                Log.Information($"✅ Tag {version} Already Exists");
            }
        });

    Target PrintInfo => _ => _
        .DependsOn(ValidateInputs)
        .Executes(() =>
        {
            Log.Information($"🔧 Image Tag: {ImageTag}");
            Log.Information($"🔧 Repository: {Repository}");
            Log.Information($"🔧 Repository Username: {RepositoryUsername}");
            Log.Information($"🔧 Git Version: {GitVersion?.SemVer ?? "Unavailable"}");
            Log.Information($"🔧 Dry Run: {DryRun}");
        });
}

record GitVersionInfo
{
    public string SemVer { get; init; }
}
