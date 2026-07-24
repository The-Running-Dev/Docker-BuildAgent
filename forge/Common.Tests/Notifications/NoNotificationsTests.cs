#nullable enable

using System;
using System.Threading.Tasks;

using Xunit;

using Notifications;
using Parameters;

namespace Common.Tests.Notifications;

/// <summary>
/// Unit tests for <see cref="NoNotifications"/>.
/// Verifies that the no-op implementation satisfies the <see cref="INotifications"/> contract
/// without performing any side effects.
/// </summary>
public class NoNotificationsTests
{
    private readonly NoNotifications _sut = new();

    // ─── Contract: returns a completed Task ──────────────────────────────────

    [Fact]
    public async Task Send_WithValidParams_CompletesSuccessfully()
    {
        var p = new NotificationParams
        {
            BuildSucceeded = true,
            Branch = "main",
            Commit = "abc123",
            Version = "1.0.0",
            BuildDuration = TimeSpan.FromSeconds(30)
        };

        await _sut.Send(p); // no exception = pass
    }

    [Fact]
    public async Task Send_WithNullParams_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(() => _sut.Send(null!));

        Assert.Null(ex);
    }

    [Fact]
    public async Task Send_WithFailedBuild_DoesNotThrow()
    {
        var p = new NotificationParams
        {
            BuildSucceeded = false,
            Branch = "feature/x",
            Commit = "def456",
            BuildDuration = TimeSpan.FromMinutes(5)
        };

        var ex = await Record.ExceptionAsync(() => _sut.Send(p));

        Assert.Null(ex);
    }

    [Fact]
    public void Send_ReturnsAlreadyCompletedTask()
    {
        var task = _sut.Send(new NotificationParams());

        Assert.True(task.IsCompleted);
        Assert.False(task.IsFaulted);
        Assert.False(task.IsCanceled);
    }

    [Fact]
    public void Send_ReturnsSameCompletedTaskInstance()
    {
        // NoNotifications.Send returns Task.CompletedTask (a static singleton).
        var task = _sut.Send(new NotificationParams());

        Assert.Equal(Task.CompletedTask, task);
    }

    // ─── Contract: implements INotifications ─────────────────────────────────

    [Fact]
    public void NoNotifications_ImplementsINotifications()
    {
        Assert.IsAssignableFrom<INotifications>(_sut);
    }

    [Fact]
    public void NoNotifications_CanBeInstantiatedWithDefaultConstructor()
    {
        var instance = new NoNotifications();

        Assert.NotNull(instance);
    }
}
