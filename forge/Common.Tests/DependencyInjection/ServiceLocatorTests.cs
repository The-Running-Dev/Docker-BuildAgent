#nullable enable

using System;

using Xunit;
using Microsoft.Extensions.DependencyInjection;

using Services;
using Notifications;
using DependencyInjection;

namespace Common.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="ServiceLocator"/> static class.
/// <para>
/// Because <see cref="ServiceLocator"/> holds a static <see cref="IServiceProvider"/>,
/// each test resets the locator via <see cref="ServiceLocator.Reset"/> in both the
/// constructor and <see cref="Dispose"/> to ensure full isolation.
/// </para>
/// </summary>
public class ServiceLocatorTests : IDisposable
{
    public ServiceLocatorTests()
    {
        // Clean state before each test (xUnit creates one instance per test method).
        ServiceLocator.Reset();
    }

    public void Dispose()
    {
        ServiceLocator.Reset();
    }

    // ─── IsInitialized ───────────────────────────────────────────────────────

    [Fact]
    public void IsInitialized_BeforeAnyInitialization_ReturnsFalse()
    {
        Assert.False(ServiceLocator.IsInitialized);
    }

    [Fact]
    public void IsInitialized_AfterInitialize_ReturnsTrue()
    {
        ServiceLocator.Initialize(EmptyProvider());

        Assert.True(ServiceLocator.IsInitialized);
    }

    [Fact]
    public void IsInitialized_AfterReset_ReturnsFalse()
    {
        ServiceLocator.Initialize(EmptyProvider());
        ServiceLocator.Reset();

        Assert.False(ServiceLocator.IsInitialized);
    }

    // ─── Initialize ──────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_WithNullProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceLocator.Initialize(null!));
    }

    [Fact]
    public void Initialize_WhenAlreadyInitialized_ThrowsInvalidOperationException()
    {
        ServiceLocator.Initialize(EmptyProvider());

        Assert.Throws<InvalidOperationException>(() => ServiceLocator.Initialize(EmptyProvider()));
    }

    [Fact]
    public void Initialize_WithValidProvider_DoesNotThrow()
    {
        var ex = Record.Exception(() => ServiceLocator.Initialize(EmptyProvider()));

        Assert.Null(ex);
    }

    // ─── InitializeWithDefaultServices ───────────────────────────────────────

    [Fact]
    public void InitializeWithDefaultServices_SetsIsInitializedToTrue()
    {
        ServiceLocator.InitializeWithDefaultServices<NoNotifications>();

        Assert.True(ServiceLocator.IsInitialized);
    }

    [Fact]
    public void InitializeWithDefaultServices_RegistersGitService()
    {
        ServiceLocator.InitializeWithDefaultServices<NoNotifications>();

        var service = ServiceLocator.GetRequiredService<GitService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void InitializeWithDefaultServices_WhenAlreadyInitialized_ThrowsInvalidOperationException()
    {
        ServiceLocator.InitializeWithDefaultServices<NoNotifications>();

        Assert.Throws<InvalidOperationException>(
            () => ServiceLocator.InitializeWithDefaultServices<NoNotifications>());
    }

    // ─── GetRequiredService ───────────────────────────────────────────────────

    [Fact]
    public void GetRequiredService_BeforeInitialization_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => ServiceLocator.GetRequiredService<GitService>());
    }

    [Fact]
    public void GetRequiredService_WhenServiceRegistered_ReturnsInstance()
    {
        var services = new ServiceCollection();
        services.AddForgeServices();
        ServiceLocator.Initialize(services.BuildServiceProvider());

        var service = ServiceLocator.GetRequiredService<GitService>();

        Assert.NotNull(service);
        Assert.IsType<GitService>(service);
    }

    [Fact]
    public void GetRequiredService_WhenServiceNotRegistered_ThrowsInvalidOperationException()
    {
        ServiceLocator.Initialize(EmptyProvider());

        Assert.Throws<InvalidOperationException>(
            () => ServiceLocator.GetRequiredService<GitService>());
    }

    // ─── GetService ───────────────────────────────────────────────────────────

    [Fact]
    public void GetService_BeforeInitialization_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(
            () => ServiceLocator.GetService<GitService>());
    }

    [Fact]
    public void GetService_WhenServiceRegistered_ReturnsInstance()
    {
        var services = new ServiceCollection();
        services.AddForgeServices();
        ServiceLocator.Initialize(services.BuildServiceProvider());

        var service = ServiceLocator.GetService<GitService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void GetService_WhenServiceNotRegistered_ThrowsInvalidOperationException()
    {
        ServiceLocator.Initialize(EmptyProvider());

        Assert.Throws<InvalidOperationException>(
            () => ServiceLocator.GetService<GitService>());
    }

    [Fact]
    public void GetService_ReturnsSameSingletonInstance_OnMultipleCalls()
    {
        var services = new ServiceCollection();
        services.AddForgeServices();
        ServiceLocator.Initialize(services.BuildServiceProvider());

        var first = ServiceLocator.GetService<GitService>();
        var second = ServiceLocator.GetService<GitService>();

        Assert.Same(first, second);
    }

    // ─── Reset ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_WhenNotInitialized_DoesNotThrow()
    {
        var ex = Record.Exception(() => ServiceLocator.Reset());

        Assert.Null(ex);
    }

    [Fact]
    public void Reset_AllowsReInitializationWithDifferentProvider()
    {
        ServiceLocator.Initialize(EmptyProvider());
        ServiceLocator.Reset();

        var services = new ServiceCollection();
        services.AddForgeServices();
        var ex = Record.Exception(() => ServiceLocator.Initialize(services.BuildServiceProvider()));

        Assert.Null(ex);
        Assert.True(ServiceLocator.IsInitialized);
    }

    [Fact]
    public void Reset_AfterReset_GetRequiredServiceThrowsInvalidOperationException()
    {
        ServiceLocator.InitializeWithDefaultServices<NoNotifications>();
        ServiceLocator.Reset();

        Assert.Throws<InvalidOperationException>(
            () => ServiceLocator.GetRequiredService<GitService>());
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IServiceProvider EmptyProvider() =>
        new ServiceCollection().BuildServiceProvider();
}
