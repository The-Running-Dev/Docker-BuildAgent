#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

using DotNetEnv;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Entities;
using Services;
using Utilities;
using Extensions;
using Parameters;
using Notifications;
using DependencyInjection;

/// <summary>
/// Represents a base class for build configurations that include parameters and notifications.
/// </summary>
/// <remarks>This class provides a framework for executing builds with customizable parameters and notification
/// settings. It includes properties for managing build state and methods for configuring and executing build
/// targets.</remarks>
/// <typeparam name="TParams">The type of parameters used for the build, which must inherit from <see cref="ForgeParams"/>.</typeparam>
/// <typeparam name="TNotifications">The type of notifications used for the build, which must implement <see cref="INotifications"/>.</typeparam>
public abstract class Base<TParams, TNotifications> : NukeBuild
    where TParams : ForgeParams, new()
    where TNotifications : class, INotifications, new()
{
    public readonly GitRepository? GitRepository =
        Directory.Exists(RootDirectory / ".git")
            ? GetGitRepositorySafely(RootDirectory)
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

    protected IServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>
    /// Gets the primary logger instance for the current build class from the dependency injection container.
    /// This logger supports both standard logging methods and custom extension methods for build status reporting.
    /// </summary>
    protected ILogger<NukeBuild> Logger => ServiceProvider?.GetRequiredService<ILogger<NukeBuild>>() ??
        throw new InvalidOperationException("ServiceProvider not initialized. Ensure OnBuildInitialized has been called.");

    ///// <summary>
    ///// Gets a typed logger instance for a specific type from the dependency injection container.
    ///// Use this method when you need a logger for a different category than the current build class.
    ///// </summary>
    ///// <typeparam name="T">The type for the logger category.</typeparam>
    ///// <returns>A typed logger instance for the specified type.</returns>
    ///// <remarks>
    ///// For general build logging, prefer using the <see cref="Log"/> property which provides
    ///// access to custom extension methods for build status reporting.
    ///// </remarks>
    //protected ILogger<T> GetLogger<T>() => ServiceProvider?.GetRequiredService<ILogger<T>>() ??
    //    throw new InvalidOperationException("ServiceProvider not initialized. Ensure OnBuildInitialized has been called.");

    /// <summary>
    /// Gets the GitService service instance from the dependency injection container.
    /// </summary>
    public GitService GitService => ServiceProvider?.GetRequiredService<GitService>() ?? 
        throw new InvalidOperationException("ServiceProvider not initialized. Ensure OnBuildInitialized has been called.");

    /// <summary>
    /// Gets the GitHubService service instance from the dependency injection container.
    /// </summary>
    public GitHubService GitHubService => ServiceProvider?.GetRequiredService<GitHubService>() ?? 
        throw new InvalidOperationException("ServiceProvider not initialized. Ensure OnBuildInitialized has been called.");

    /// <summary>
    /// Gets the notifications service instance from the dependency injection container.
    /// </summary>
    protected TNotifications NotificationService => ServiceProvider?.GetRequiredService<TNotifications>() ?? 
        throw new InvalidOperationException("ServiceProvider not initialized. Ensure OnBuildInitialized has been called.");

    /// <summary>
    /// Initializes the build parameters and configuration settings.
    /// </summary>
    /// <remarks>This method sets up the build parameters using the specified root directory and repository
    /// URL. It also configures the build environment by invoking the <see cref="Configure"/> method.</remarks>
    protected override void OnBuildInitialized()
    {
        // Initialize dependency injection container
        InitializeDependencyInjection();

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

        // Only proceed with notifications if ServiceProvider is initialized
        if (ServiceProvider == null)
        {
            return;
        }

        TNotifications notifications;
        try
        {
            notifications = NotificationService;
        }
        catch (InvalidOperationException)
        {
            // ServiceProvider not properly initialized, skip notifications
            return;
        }

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
                Branch = GitRepository?.Branch ?? "Unknown",
                Version = Parameters?.Version?.ToString() ?? "Unknown",
                WebHookUrl = NotificationsWebHookUrl
            };
            p.Urls = GitService.GetUrls(p.Branch, p.Commit);

            notifications.Send(p).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Builds the specified targets for a Nuke build configuration.
    /// </summary>
    /// <remarks>This method initializes the build environment by generating and loading environment files,
    /// and sets up the GitService safe directory. It then executes the specified build targets.</remarks>
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
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [ERR] [ERROR] Build Env Incomplete...(See {config.EnvMapFile})");

            return -1;
        }

        if (File.Exists(config.EnvFilePath))
        {
            Env.Load(config.EnvFilePath);

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [INF] [OK] Loaded Build Env...{config.EnvFile}");
        }

        // Initialize service locator for static access
        if (!ServiceLocator.IsInitialized)
        {
            ServiceLocator.InitializeWithDefaultServices<NoNotifications>();
        }

        try
        {
            ServiceLocator.GetRequiredService<GitService>().SetSafeDirectory(RootDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [WRN] Could not set GitService safe directory: {ex.Message}");
        }

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
        if (Parameters?.Config?.EnvFilePath != null && File.Exists(Parameters.Config.EnvFilePath))
        {
            File.Delete(Parameters.Config.EnvFilePath);
        }
    }

    /// <summary>
    /// Initializes the dependency injection container with the required services.
    /// </summary>
    /// <remarks>This method sets up the DI container and can be overridden to customize service registration.</remarks>
    protected virtual void InitializeDependencyInjection()
    {
        var services = new ServiceCollection();
        
        // Add forge services
        services.AddForgeServices();
        services.AddNotificationServices<TNotifications>();
        
        // Allow derived classes to add additional services
        ConfigureServices(services);
        
        ServiceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Configures additional services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <remarks>Override this method in derived classes to register additional services.</remarks>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Default implementation does nothing - override in derived classes to add custom services
    }

    /// <summary>
    /// Safely attempts to create a GitRepository from a local directory, handling the case where no commits exist.
    /// </summary>
    /// <param name="directory">The directory path to create the GitRepository from.</param>
    /// <returns>A GitRepository instance if successful, null if no commits exist or an error occurs.</returns>
    private static GitRepository? GetGitRepositorySafely(string directory)
    {
        try
        {
            return GitRepository.FromLocalDirectory(directory);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("Could not find commit information"))
        {
            // Use Console.WriteLine for static context since logger is not available
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [WRN] GitService Repository Exists but Has no Commits Yet. Some GitService-related Features May Not Work Properly.");

            return null;
        }
        catch (Exception ex)
        {
            // Use Console.WriteLine for static context since logger is not available
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} [WRN] Failed to Load GitService Repository: {ex.Message}");

            return null;
        }
    }

    public Target Setup => _ => _
        .Executes(() =>
        {
            if (!Directory.Exists(Config.DirectoryPath))
            {
                Logger.LogError("Config Directory Does Not Exist...");

                Assert.Fail($"[ERROR] Config Directory Does Not Exist...");
            }

            Logger.LogInformation($"{Environment.NewLine}{Parameters.ToDisplayString()}");
        });
}