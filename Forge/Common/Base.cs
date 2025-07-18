#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using Serilog;
using DotNetEnv;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Execution;

using Entities;
using Extensions;
using Parameters;
using Notifications;

using Utilities;

/// <summary>
/// Represents a base class for build configurations that include parameters and notifications.
/// </summary>
/// <remarks>This class provides a framework for executing builds with customizable parameters and notification
/// settings. It includes properties for managing build state and methods for configuring and executing build
/// targets.</remarks>
/// <typeparam name="TParams">The type of parameters used for the build, which must inherit from <see cref="ForgeParameters"/>.</typeparam>
/// <typeparam name="TNotifications">The type of notifications used for the build, which must implement <see cref="INotifications"/>.</typeparam>
public abstract class Base<TParams, TNotifications> : NukeBuild
    where TParams : ForgeParams, new()
    where TNotifications : INotifications, new()
{
    public readonly GitRepository? GitRepository =
        Directory.Exists(RootDirectory / ".git")
            ? GitRepository.FromLocalDirectory(RootDirectory)
            : null;
    
    [Parameter("Enable Notifications for the Build")]
    public readonly bool Notifications = true;

    [Parameter("Force Notifications for Local Builds")]
    public readonly bool ForceNotifications;

    [Parameter("WebHook URL for Notifications")]
    [Secret]
    public readonly string? NotificationsWebHookUrl;

    [Parameter("Enable Dry Run (Skip Push and Tag)")]
    public readonly bool DryRun;

    [Parameter("Force Push/Tag for Local Builds")]
    public readonly bool ForcePush;

    DateTime BuildStartTime { get; set; } = DateTime.UtcNow;

    TimeSpan BuildDuration { get; set; }

    bool BuildSucceeded { get; set; } = true;

    public TParams Parameters { get; protected set; } = new();

    public BuildConfig Config { get; protected set; } = new(RootDirectory);

    /// <summary>
    /// Initializes the build parameters and configuration settings.
    /// </summary>
    /// <remarks>This method sets up the build parameters using the specified root directory and repository
    /// URL. It also configures the build environment by invoking the <see cref="Configure"/> method.</remarks>
    protected override void OnBuildInitialized()
    {
        Parameters = new TParams
        {
            Config = new BuildConfig(RootDirectory),
            RootDirectory = RootDirectory,
            RepositoryUrl = GitRepository?.HttpsUrl?.Replace(".git", "") ?? "Unknown",
            Version = Common.GetVersion(RootDirectory),
            Verbosity = Verbosity
        };
        
        Configure();
    }

    /// <summary>
    /// Finalizes the build process by performing cleanup operations and sending notifications if configured.
    /// </summary>
    /// <remarks>This method calculates the total build duration and determines the success status of the
    /// build. If notifications are enabled and the build is not a local or dry run, it sends a notification with the
    /// build details using the configured webhook URL.</remarks>
    protected override void OnBuildFinished()
    {
        Cleanup();

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

    /// <summary>
    /// Builds the specified targets for a Nuke build configuration.
    /// </summary>
    /// <remarks>This method initializes the build environment by generating and loading environment files,
    /// and sets up the Git safe directory. It then executes the specified build targets.</remarks>
    /// <typeparam name="T">The type of the Nuke build, which must be a subclass of <see cref="NukeBuild"/> and have a parameterless
    /// constructor.</typeparam>
    /// <param name="targets">An array of expressions representing the build targets to execute.</param>
    /// <returns>An integer indicating the result of the build process. Returns -1 if the environment setup is incomplete;
    /// otherwise, returns the result of executing the targets.</returns>
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

    /// <summary>
    /// Configures the settings or behavior of the current instance.
    /// </summary>
    /// <remarks>This method is intended to be overridden in a derived class to provide specific configuration
    /// logic. By default, it performs no operation.</remarks>
    protected virtual void Configure() { }

    /// <summary>
    /// Performs cleanup operations by deleting the environment configuration file.
    /// </summary>
    /// <remarks>This method deletes the file specified by the <see cref="Parameters.Config.EnvFilePath"/>
    /// property. Override this method in a derived class to implement additional cleanup logic.</remarks>
    protected virtual void Cleanup()
    {
        File.Delete(Parameters.Config.EnvFilePath);
    }

    public Target Setup => _ => _
        .Executes(() =>
        {
            if (!Directory.Exists(Config.DirectoryPath))
            {
                Assert.Fail($"❌ Config Directory Does Not Exist...");
            }

            //Parameters = new TParams
            //{
            //    Config = new BuildConfig(RootDirectory),
            //    DryRun = DryRun,
            //    ForcePush = ForcePush,
            //    ForceNotifications = ForceNotifications,
            //    Notifications = Notifications,
            //    NotificationsWebHookUrl = NotificationsWebHookUrl,
            //    RootDirectory = RootDirectory,
            //    RepositoryUrl = GitRepository?.HttpsUrl?.Replace(".git", "") ?? "Unknown",
            //    Verbosity = Verbosity,
            //    Version = Common.GetVersion(RootDirectory)
            //};

            //Configure();

            Log.Information($"{Environment.NewLine}{Parameters.ToDisplayString()}");
        });
}