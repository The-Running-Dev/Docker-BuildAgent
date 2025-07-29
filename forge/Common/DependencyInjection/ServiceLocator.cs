#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;

using Notifications;

namespace DependencyInjection;

/// <summary>
/// Provides service location functionality for accessing dependency injection services.
/// </summary>
/// <remarks>
/// This class acts as a service locator to provide access to services registered in the DI container.
/// While service locator is generally an anti-pattern, it's used here for backward compatibility
/// with existing static service usage in the Nuke build system.
/// </remarks>
public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets a value indicating whether the service provider has been initialized.
    /// </summary>
    public static bool IsInitialized => _serviceProvider != null;

    /// <summary>
    /// Initializes the service locator with the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use for service resolution.</param>
    /// <exception cref="ArgumentNullException">Thrown when serviceProvider is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the service locator is already initialized.</exception>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        
        if (_serviceProvider != null)
        {
            throw new InvalidOperationException("ServiceLocator is already initialized.");
        }

        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Initializes the service locator with default Forge services.
    /// </summary>
    /// <typeparam name="TNotifications">The type of notifications service to use.</typeparam>
    public static void InitializeWithDefaultServices<TNotifications>()
        where TNotifications : class, INotifications, new()
    {
        var serviceProvider = ServiceCollectionExtensions.CreateForgeServiceProvider<TNotifications>();
        
        Initialize(serviceProvider);
    }

    /// <summary>
    /// Gets a service of the specified type from the service provider.
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve.</typeparam>
    /// <returns>The service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service locator is not initialized.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the requested service is not registered.</exception>
    public static T GetService<T>() where T : notnull
    {
        EnsureInitialized();
        
        var service = _serviceProvider!.GetService<T>();
        
        if (service == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
        }

        return service;
    }

    /// <summary>
    /// Gets a required service of the specified type from the service provider.
    /// </summary>
    /// <typeparam name="T">The type of service to retrieve.</typeparam>
    /// <returns>The service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service locator is not initialized.</exception>
    public static T GetRequiredService<T>() where T : notnull
    {
        EnsureInitialized();

        return _serviceProvider!.GetRequiredService<T>();
    }

    /// <summary>
    /// Resets the service locator, clearing the current service provider.
    /// </summary>
    /// <remarks>
    /// This method is primarily intended for testing scenarios where you need to reinitialize
    /// the service locator with different services.
    /// </remarks>
    public static void Reset()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        _serviceProvider = null;
    }

    /// <summary>
    /// Ensures that the service locator has been initialized.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the service locator is not initialized.</exception>
    private static void EnsureInitialized()
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException(
                "ServiceLocator is not initialized. Call Initialize() or InitializeWithDefaultServices() first.");
        }
    }
}
