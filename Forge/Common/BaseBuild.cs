using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using DotNetEnv;

using Entities;

using Extensions;

using Notifications;

using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;

using Parameters;

using Serilog;

using Utilities;

/// <summary>
/// Represents a base class for build configurations that include parameters and notifications.
/// </summary>
/// <remarks>This class provides a framework for executing builds with customizable parameters and notification
/// settings. It includes properties for managing build state and methods for configuring and executing build
/// targets.</remarks>
/// <typeparam name="TParams">The type of parameters used for the build, which must inherit from <see cref="ForgeParameters"/>.</typeparam>
/// <typeparam name="TNotifications">The type of notifications used for the build, which must implement <see cref="INotifications"/>.</typeparam>
public abstract class BaseBuild<TParams, TNotifications> : NukeBuild
    where TParams : ForgeParams, new()
    where TNotifications : INotifications, new()
{
    [GitRepository]
    public readonly GitRepository GitRepository;
    
    [Parameter("Send Notifications")]
    public readonly bool Notifications = true;

    [Parameter("Force Notifications for Local Builds")]
    public readonly bool ForceNotifications;

    [Parameter("WebHook URL for Notifications")]
    [Secret]
    public readonly string NotificationsWebHookUrl;

    [Parameter("Enable Dry Run (Skip Push and Tag)")]
    public readonly bool DryRun;

    [Parameter("Force Push/Tag for Local Builds")]
    public readonly bool ForcePush;

    DateTime BuildStartTime { get; set; } = DateTime.UtcNow;

    TimeSpan BuildDuration { get; set; }

    bool BuildSucceeded { get; set; } = true;

    public TParams Parameters { get; protected set; }

    public BuildConfig Config { get; protected set; } = new(RootDirectory);

    protected override void OnBuildFinished()
    {
        Cleanup(Parameters);

        var notifications = new TNotifications();

        BuildDuration = DateTime.UtcNow - BuildStartTime;

        var notificationsAreEnabled = Notifications && !string.IsNullOrEmpty(NotificationsWebHookUrl);

        if (ExecutionPlan.Any(x => x.Status == ExecutionStatus.Failed))
        {
            BuildSucceeded = false;
        }

        if (notificationsAreEnabled && (ForceNotifications || (!IsLocalBuild && !DryRun)))
        {
            var p = new NotificationParams
            {
                BuildDuration = BuildDuration,
                BuildSucceeded = BuildSucceeded,
                Commit = GitRepository?.Commit ?? "Unknown",
                Version = Parameters?.Version?.ToString() ?? "Unknown",
                WebHookUrl = NotificationsWebHookUrl
            };

            notifications.Send(p).GetAwaiter().GetResult();
        }
    }

    protected static int Build<T>(params Expression<Func<T, Target>>[] targets) where T : NukeBuild, new()
    {
        var config = new BuildConfig(RootDirectory);
        var isSuccessful = Files.GenerateEnvironmentFile(
            config.EnvMapFilePath,
            config.EnvFilePath,
            msg => Console.WriteLine($"{DateTime.Now:HH:mm:ss} [INF] {msg}"),
            msg => Console.WriteLine($"{DateTime.Now:HH:mm:ss} [WRN] {msg}"));

        if (!isSuccessful)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ERR] ❌ Build Env Incomplete...(See {config.EnvMapFile})");

            return -1;
        }

        if (File.Exists(config.EnvFilePath))
        {
            Env.Load(config.EnvFilePath);

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [INF] ✅ Loaded Build Env...{config.EnvFile}");
        }

        Git.SetSafeDirectory(RootDirectory);

        return Execute(targets);
    }

    protected virtual void Configure(TParams p) { }

    protected virtual void Cleanup(TParams p)
    {
        File.Delete(p.Config.EnvFilePath);
    }

    public Target Setup => _ => _
        .Executes(() =>
        {
            if (!Directory.Exists(Config.DirectoryPath))
            {
                Assert.Fail($"❌ Config Directory Does Not Exist...");
            }

            Parameters = new TParams
            {
                Config = new BuildConfig(RootDirectory),
                DryRun = DryRun,
                ForcePush = ForcePush,
                ForceNotifications = ForceNotifications,
                Notifications = Notifications,
                NotificationsWebHookUrl = NotificationsWebHookUrl,
                RootDirectory = RootDirectory,
                RepositoryUrl = GitRepository?.HttpsUrl?.Replace(".git", "") ?? "Unknown",
                Verbosity = Verbosity,
                Version = Common.GetVersion(RootDirectory)
            };

            Configure(Parameters);

            Log.Information($"{Environment.NewLine}{Parameters.ToDisplayString()}");
        });
}