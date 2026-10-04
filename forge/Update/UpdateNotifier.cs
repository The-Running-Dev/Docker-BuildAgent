#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Update;

/// <summary>
/// The two conditions the Updater's own webhook sender answers (design/20-contract.md § Notifications). They mirror
/// the conditions Build types answer, through a type of the Updater's own: the two share the webhook setting's
/// name (<c>NotificationsWebHookUrl</c>) and no code (2026-10-03 decision), so this project takes no reference
/// into Build types.
/// </summary>
public enum UpdateNotificationErrorCode
{
    /// <summary>The webhook did not accept the payload: unreachable, slow, or answered with a failure status.</summary>
    DeliveryFailed,

    /// <summary>A notification was requested with no URL.</summary>
    NotConfigured,
}

/// <summary>A notification that did not go out. <see cref="Message"/> never contains the webhook URL (I44).</summary>
public sealed record UpdateNotificationError(UpdateNotificationErrorCode Code, string Message);

/// <summary>The result of one notification attempt. A failure is a value, never an exception, because it must
/// never change the update's exit status (I44).</summary>
public sealed record UpdateNotificationResult(UpdateNotificationError? Error)
{
    public bool Delivered => Error == null;

    public static UpdateNotificationResult Success { get; } = new((UpdateNotificationError?)null);

    public static UpdateNotificationResult DeliveryFailed(string message) =>
        new(new UpdateNotificationError(UpdateNotificationErrorCode.DeliveryFailed, message));

    public static UpdateNotificationResult NotConfigured() =>
        new(new UpdateNotificationError(UpdateNotificationErrorCode.NotConfigured,
            "A notification was requested but no webhook URL was given."));
}

/// <summary>What the webhook is told: the target and how the update ended.</summary>
public sealed record UpdateNotification(string ContainerName, UpdateOutcome Outcome);

/// <summary>Sends the update's notification. Implementations never throw for a delivery problem and never put the
/// URL, or any part of it, in a result's message (I44).</summary>
public interface IUpdateNotifier
{
    Task<UpdateNotificationResult> SendAsync(string? webhookUrl, UpdateNotification notification);
}

/// <summary>
/// The Updater's own small webhook sender (design/10-design.md § Module boundaries). It posts a Discord-style
/// embed, the same shape the Build types' notifications use, and shares no code with them. The attempt is bounded by
/// <see cref="Timeout"/>, so a slow webhook cannot hang the command (I44).
/// </summary>
public sealed class WebhookUpdateNotifier : IUpdateNotifier
{
    // Not specified by the design, which says only that a slow notification must not change the exit status.
    // This is this slice's own bounded choice: long enough for a normal webhook, short enough to not stall a script.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpMessageHandler? _handler;

    public TimeSpan Timeout { get; }

    /// <param name="handler">Replaces the network, for tests. The notifier does not dispose it.</param>
    /// <param name="timeout">Bounds one attempt; defaults to <see cref="DefaultTimeout"/>.</param>
    public WebhookUpdateNotifier(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _handler = handler;
        Timeout = timeout ?? DefaultTimeout;
    }

    public async Task<UpdateNotificationResult> SendAsync(string? webhookUrl, UpdateNotification notification)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return UpdateNotificationResult.NotConfigured();
        }

        if (!Uri.TryCreate(webhookUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return UpdateNotificationResult.DeliveryFailed("The webhook address is not an http or https URL.");
        }

        try
        {
            using var cancellation = new CancellationTokenSource(Timeout);
            using var client = _handler == null
                ? new HttpClient()
                : new HttpClient(_handler, disposeHandler: false);
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;

            using var content = new StringContent(BuildPayload(notification), Encoding.UTF8, "application/json");

            // WaitAsync bounds the attempt even when a handler ignores the cancellation token.
            using var response = await client.PostAsync(uri, content, cancellation.Token).WaitAsync(Timeout);

            return response.IsSuccessStatusCode
                ? UpdateNotificationResult.Success
                : UpdateNotificationResult.DeliveryFailed(
                    $"The webhook answered with status {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            return UpdateNotificationResult.DeliveryFailed(
                $"The webhook did not answer within {Timeout.TotalSeconds:0.###} seconds.");
        }
        catch (Exception ex)
        {
            // Only the type is reported: a transport exception's message can carry the host or the whole URL.
            return UpdateNotificationResult.DeliveryFailed($"The webhook could not be reached ({ex.GetType().Name}).");
        }
    }

    /// <summary>The embed: a title that says how the update ended, the container, and the outcome by name.</summary>
    internal static string BuildPayload(UpdateNotification notification)
    {
        var succeeded = notification.Outcome is UpdateOutcome.Succeeded or UpdateOutcome.AlreadyCurrent;
        var title = notification.Outcome switch
        {
            UpdateOutcome.Succeeded => "Update succeeded",
            UpdateOutcome.AlreadyCurrent => "Already up to date",
            UpdateOutcome.RestoredAfterUnhealthy => "Update failed, prior container restored",
            UpdateOutcome.UnhealthyNotRestored => "Update failed, replacement left in place",
            UpdateOutcome.RestoreFailed => "Update failed, restore failed",
            _ => "Update failed",
        };

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title,
                    description = $"**Container:** `{notification.ContainerName}`\n**Outcome:** `{notification.Outcome}`",
                    color = succeeded ? 0x57F287 : 0xED4245,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    footer = new { text = "Docker-BuildAgent updater" },
                },
            },
        };

        return JsonSerializer.Serialize(payload);
    }
}
