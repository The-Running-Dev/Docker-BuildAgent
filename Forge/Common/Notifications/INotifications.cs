using System.Threading.Tasks;

using Parameters;

namespace Notifications;

/// <summary>
/// Represents a notification mechanism that can send messages using specified parameters.
/// </summary>
/// <remarks>Implementations of this interface define how notifications are sent, which may vary based on the
/// underlying communication method (e.g., email, SMS, push notifications).</remarks>
public interface INotifications
{
    /// <summary>
    /// Sends a notification based on the specified parameters.
    /// </summary>
    /// <param name="p">The parameters that define the notification to be sent. This includes details such as the recipient, message
    /// content, and any additional options.</param>
    /// <returns>A task that represents the asynchronous operation of sending the notification.</returns>
    public Task Send(NotificationParams p);
}