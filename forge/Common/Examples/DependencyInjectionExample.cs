#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Services;
using Notifications;
using DependencyInjection;

namespace Examples;

/// <summary>
/// Demonstrates how to use the Dependency Injection container in the Forge build system.
/// </summary>
/// <remarks>
/// This class shows various ways to configure and use dependency injection:
/// 1. Using the service locator pattern (for backward compatibility)
/// 2. Using dependency injection in custom build classes
/// 3. Adding custom services to the DI container
/// </remarks>
public class DependencyInjectionExample
{
    /// <summary>
    /// Example 1: Using the service locator pattern for simple scenarios.
    /// </summary>
    public static void Example1_ServiceLocator()
    {
        // Initialize the service locator with default services
        ServiceLocator.InitializeWithDefaultServices<NoNotifications>();

        // Get services from the locator
        var gitService = ServiceLocator.GetRequiredService<GitService>();
        var gitHubService = ServiceLocator.GetRequiredService<GitHubService>();

        Console.WriteLine($"Got GitService service: {gitService.GetType().Name}");
        Console.WriteLine($"Got GitHubService service: {gitHubService.GetType().Name}");
        
        // Clean up
        ServiceLocator.Reset();
    }

    /// <summary>
    /// Example 2: Creating a custom service provider with additional services.
    /// </summary>
    public static void Example2_CustomServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Add forge services
        services.AddForgeServices();
        services.AddNotificationServices<NoNotifications>();
        
        // Add your custom services
        services.AddSingleton<ICustomService, CustomService>();
        
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        
        // Use the services
        var gitService = serviceProvider.GetRequiredService<GitService>();
        var customService = serviceProvider.GetRequiredService<ICustomService>();
        
        Console.WriteLine($"Got GitService service: {gitService.GetType().Name}");
        Console.WriteLine($"Got custom service: {customService.GetType().Name}");
        
        // Clean up
        serviceProvider.Dispose();
    }

    /// <summary>
    /// Example 3: Using the host builder pattern for more advanced scenarios.
    /// </summary>
    public static async Task Example3_HostBuilder()
    {
        var host = ServiceCollectionExtensions.CreateForgeHostBuilder<NoNotifications>()
            .ConfigureServices((context, services) =>
            {
                // Add additional services here
                services.AddSingleton<ICustomService, CustomService>();
            })
            .Build();

        // Use the services
        var gitService = host.Services.GetRequiredService<GitService>();
        var logger = host.Services.GetRequiredService<ILogger<DependencyInjectionExample>>();
        
        logger.LogInformation("Got GitService service: {ServiceType}", gitService.GetType().Name);
        
        await host.StopAsync();
        host.Dispose();
    }
}

/// <summary>
/// Example interface for demonstrating custom service registration.
/// </summary>
public interface ICustomService
{
    string GetCustomData();
}

/// <summary>
/// Example implementation of a custom service.
/// </summary>
public class CustomService : ICustomService
{
    public string GetCustomData()
    {
        return "Custom data from DI container";
    }
}
