#nullable enable

using System.Threading.Tasks;

using Parameters;

namespace Notifications;

/// <summary>
/// A no-op notifications implementation for scenarios where notifications are not needed.
/// </summary>
/// <remarks>
/// This class provides a default implementation of INotifications that does nothing.
/// It's used as a fallback when no specific notification service is configured.
/// </remarks>
public class NoNotifications : INotifications
{
    /// <summary>
    /// Does nothing and returns a completed task.
    /// </summary>
    /// <param name="parameters">The notification parameters (ignored).</param>
    /// <returns>A completed task.</returns>
    public Task Send(NotificationParams parameters)
    {
        return Task.CompletedTask;
    }
}
