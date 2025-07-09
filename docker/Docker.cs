using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

using DotNetEnv;

using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Git;
using Nuke.Common.Tooling;
using Nuke.Common.Execution;
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

    [Parameter("Webhook URL for Build Notifications")]
    [Secret]
    readonly string BuildNotificationsWebhookUrl;

    [Parameter("Dockerfile Path")]
    readonly string Dockerfile = "Dockerfile";

    [Parameter("Enable Dry Run (Skip Push and Tag)")]
    readonly bool DryRun;

    [GitRepository] readonly GitRepository GitRepository;

    GitVersionInfo GitVersion;

    AbsolutePath VersionFile => RootDirectory / ".resolved-version";

    [Parameter("Force Push/Tag Even During Local Builds")]
    readonly bool ForcePush;

    [Parameter("Send Notifications")]
    readonly bool SendNotifications = true;

    [Parameter("Force Notifications Even During Local Builds")]
    readonly bool ForceNotifications;

    DateTime BuildStartTime { get; set; } = DateTime.UtcNow;

    TimeSpan BuildDuration { get; set; }

    bool BuildSucceeded { get; set; } = true;

    string GetResolvedVersion()
    {
        if (!VersionFile.Exists())
        {
            Assert.Fail($"❌ {VersionFile} is Missing. Run GetVersion First.");
        }

        return VersionFile.ReadAllText().Trim();
    }

    async Task SendNotification()
    {
        if (string.IsNullOrWhiteSpace(BuildNotificationsWebhookUrl))
        {
            Log.Warning("⚠️ Build Notifications Webhook Url is Not Set.");

            return;
        }

        var title = BuildSucceeded ? "✅ Build Succeeded" : "❌ Build Failed";
        var color = BuildSucceeded ? 0x57F287 : 0xED4245;

        var gitCommit = GitRepository?.Commit ?? "unknown";
        var gitCommitMessage = ProcessTasks
            .StartProcess("git", "log -1 --pretty=%B", logOutput: false)
            .AssertZeroExitCode()
            .Output
            .Select(o => o.Text.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(20)
            .ToList();

        var formattedCommitMessage = string.Join("\n", gitCommitMessage);
        var gitBranch = GitRepository?.Branch ?? "unknown";
        var durationText = BuildDuration.TotalMinutes >= 1
            ? $"{BuildDuration.TotalMinutes:N1}m"
            : $"{BuildDuration.TotalSeconds:N0}s";

        // Use Git.GetRepoBranchCommitUrls to get URLs
        var (repoUrl, branchUrl, commitUrl) = Git.GetRepoBranchCommitUrls(gitBranch, gitCommit);
        var branchLink = branchUrl != null ? $"[`{gitBranch}`]({branchUrl})" : $"`{gitBranch}`";
        var commitLink = commitUrl != null ? $"[`{gitCommit}`]({commitUrl})" : $"`{gitCommit}`";

        var description =
            $"**Branch:** {branchLink}\n" +
            $"**Commit:** {commitLink}\n" +
            $"**Version:** `{GitVersion?.SemVer ?? "N/A"}`\n" +
            $"**Message:**\n```{formattedCommitMessage}```\n" +
            $"**Duration:** `{durationText}`";

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title,
                    description,
                    color,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    footer = new { text = "NUKE Build System" }
                }
            }
        };

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(BuildNotificationsWebhookUrl, payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            Log.Error($"❌ Failed to Send Discord Notification: {response.StatusCode}\n{error}");
        }
    }

    protected override void OnBuildFinished()
    {
        BuildDuration = DateTime.UtcNow - BuildStartTime;
        var notificationsAreEnabled = SendNotifications && !string.IsNullOrEmpty(BuildNotificationsWebhookUrl);

        if (ExecutionPlan.Any(x => x.Status == ExecutionStatus.Failed))
        {
            BuildSucceeded = false;
        }

        if (notificationsAreEnabled && (ForceNotifications || (!IsLocalBuild && !DryRun)))
        {
            Log.Information($"Sending...{BuildNotificationsWebhookUrl}");
            SendNotification().GetAwaiter().GetResult();
        }
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

    Target Publish => _ => _
        .DependsOn(Push)
        .Executes(() =>
        {
            Log.Information("✅ Publish Step Complete");
        });

    Target Push => _ => _
        .DependsOn(Tag)
        .OnlyWhenDynamic(() => ForcePush || (!IsLocalBuild && !DryRun))
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
        .OnlyWhenDynamic(() => ForcePush || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            var version = $"v{GetResolvedVersion()}";
            var existingTags = GitTasks.Git("tag");

            if (existingTags.All(l => l.Text != version))
            {
                GitTasks.Git($"tag -f {version}");

                GitTasks.Git($"push origin -f {version}");

                Log.Information($"🏷️ Created and Pushed Tag: {version}");
            }
            else
            {
                Log.Information($"✅ Tag {version} Already Exists");
            }
        });

    Target BuildContainer => _ => _
        .DependsOn(PrintInfo)
        .Executes(() =>
        {
            var version = GetResolvedVersion();

            Log.Information($"Building: {RootDirectory / Dockerfile}, Tag: {Repository}/{ImageTag}:latest...");

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

    Target PrintInfo => _ => _
        .DependsOn(ValidateInputs)
        .Executes(() =>
        {
            Log.Information($"🔧 Image Tag: {ImageTag}");
            Log.Information($"🔧 Repository: {Repository}");
            Log.Information($"🔧 Repository Username: {RepositoryUsername}");
            Log.Information($"🔧 Git Version: {GitVersion?.SemVer ?? "Unavailable"}");
            Log.Information($"🔧 Force Push: {ForcePush}");
            Log.Information($"🔧 Send Notifications: {SendNotifications}");
            Log.Information($"🔧 Force Notifications: {ForceNotifications}");
            Log.Information($"🔧 Dry Run: {DryRun}");
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

    Target GetChangeLog => _ => _
        .Executes(() =>
        {
            var changelogPath = "CHANGELOG.md";
            var changeLog = Git.GetChangelog();

            if (string.IsNullOrEmpty(changeLog))
            {
                Log.Information("No New Commits Since Last Tag...");

                return;
            }

            var today = DateTime.UtcNow.ToString("yyyy.MM.dd");
            var changelogBuilder = new StringBuilder();
            changelogBuilder.AppendLine($"## {today}\n");

            // Append the rest of the old changelog (if any)
            var oldChangelog = File.Exists(changelogPath) ? File.ReadAllText(changelogPath) : "";
            changelogBuilder.AppendLine(oldChangelog);

            File.WriteAllText(changelogPath, changelogBuilder.ToString());
        });    

    Target CreateRelease => _ => _
        .DependsOn(Push, GetChangeLog)
        .OnlyWhenDynamic(() => !string.IsNullOrWhiteSpace(Repository) && !string.IsNullOrWhiteSpace(RepositoryToken))
        .Executes(async () =>
        {
            var version = $"v{GetResolvedVersion()}";
            var changelogPath = "CHANGELOG.md";
            var releaseNotes = File.Exists(changelogPath) ? File.ReadAllText(changelogPath) : "No changelog available.";

            // Compose Docker image asset links
            var dockerImageLatest = $"{Repository}/{ImageTag}:latest";
            var dockerImageVersion = $"{Repository}/{ImageTag}:{GetResolvedVersion()}";
            var assetsText =
                $"**Docker Images:**\n" +
                $"- `{dockerImageLatest}`\n" +
                $"- `{dockerImageVersion}`\n\n";

            var body = assetsText + releaseNotes;

            var apiUrl = $"https://api.github.com/repos/{Repository}/releases";
            var tagName = version;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NukeBuild", "1.0"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", RepositoryToken);

            var payload = new
            {
                tag_name = tagName,
                name = tagName,
                body = body,
                draft = false,
                prerelease = false
            };

            var response = await client.PostAsJsonAsync(apiUrl, payload);

            if (response.IsSuccessStatusCode)
            {
                Log.Information($"🚀 GitHub Release '{tagName}' Created Successfully.");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();

                Log.Error($"❌ Failed to Create GitHub Release: {response.StatusCode}\n{error}");
            }
        });
}