#nullable enable

using System;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Services;
using Notifications;

namespace DependencyInjection;

/// <summary>
/// Provides extension methods for configuring dependency injection services for the Forge build system.
/// </summary>
/// <remarks>
/// This class contains methods to register all the necessary services, utilities, and configurations
/// required by the Forge build system into the dependency injection container.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Forge-related services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddForgeServices(this IServiceCollection services)
    {
        // Core services - register both interface and concrete type for backward compatibility
        services.AddSingleton<GitService>();
        services.AddSingleton<IGitService>(provider => provider.GetRequiredService<GitService>());
        services.AddSingleton<GitHubService>();
        services.AddSingleton<IGitHubService>(provider => provider.GetRequiredService<GitHubService>());
        services.AddScoped<IChangeLogConfigService, ChangeLogConfigService>();
        
        // Node and Docker services
        services.AddNodeServices();
        services.AddDockerServices();
        
        // Add logging with custom console formatter
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole(options =>
            {
                options.FormatterName = "forge";
            });
            builder.AddConsoleFormatter<ForgeConsoleFormatter, ForgeConsoleFormatterOptions>();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services;
    }

    /// <summary>
    /// Adds notification services to the dependency injection container.
    /// </summary>
    /// <typeparam name="TNotifications">The type of notifications service to register.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNotificationServices<TNotifications>(this IServiceCollection services)
        where TNotifications : class, INotifications, new()
    {
        services.AddSingleton<INotifications, TNotifications>();
        services.AddSingleton<TNotifications>();

        return services;
    }

    /// <summary>
    /// Creates a configured service provider with all Forge services registered.
    /// </summary>
    /// <typeparam name="TNotifications">The type of notifications service to use.</typeparam>
    /// <returns>A configured service provider.</returns>
    public static IServiceProvider CreateForgeServiceProvider<TNotifications>()
        where TNotifications : class, INotifications, new()
    {
        var services = new ServiceCollection();
        
        services.AddForgeServices();
        services.AddNotificationServices<TNotifications>();
        
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a host builder configured with Forge services.
    /// </summary>
    /// <typeparam name="TNotifications">The type of notifications service to use.</typeparam>
    /// <returns>A configured host builder.</returns>
    public static IHostBuilder CreateForgeHostBuilder<TNotifications>()
        where TNotifications : class, INotifications, new()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddForgeServices();
                services.AddNotificationServices<TNotifications>();
            });
    }

    /// <summary>
    /// Adds Node.js related services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddNodeServices(this IServiceCollection services)
    {
        // Register both interface and concrete type for backward compatibility
        services.AddScoped<NodeService>();
        services.AddScoped<INodeService>(provider => provider.GetRequiredService<NodeService>());

        return services;
    }

    /// <summary>
    /// Adds Docker related services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddDockerServices(this IServiceCollection services)
    {
        // Register both interface and concrete type for backward compatibility
        services.AddScoped<DockerService>();
        services.AddScoped<IDockerService>(provider => provider.GetRequiredService<DockerService>());

        return services;
    }
}

/// <summary>
/// Custom console formatter options for Forge build system.
/// </summary>
public sealed class ForgeConsoleFormatterOptions : ConsoleFormatterOptions
{
    public ForgeConsoleFormatterOptions()
    {
        IncludeScopes = false;
        TimestampFormat = "HH:mm:ss ";
        UseUtcTimestamp = false;
    }
}

/// <summary>
/// Custom console formatter that matches the Forge build system log format.
/// </summary>
public sealed class ForgeConsoleFormatter : ConsoleFormatter
{
    private readonly ForgeConsoleFormatterOptions _options;

    public ForgeConsoleFormatter(IOptionsMonitor<ForgeConsoleFormatterOptions> options)
        : base("forge")
    {
        _options = options.CurrentValue;
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        // Get timestamp
        var timestamp = DateTime.Now.ToString(_options.TimestampFormat ?? "HH:mm:ss ");

        // Get log level abbreviation
        var logLevel = GetLogLevelString(logEntry.LogLevel);

        // Format: "20:38:01 [INF] message" 
        textWriter.WriteLine($"{timestamp}[{logLevel}] {message}");

        // Write exception if present
        if (logEntry.Exception != null)
        {
            textWriter.WriteLine($"{timestamp}[{logLevel}] {logEntry.Exception}");
        }
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG", 
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            LogLevel.None => "NON",
            _ => "UNK"
        };
    }
}
