using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Serilog;
using Nuke.Common.Tooling;

using Parameters;

namespace Notifications;

/// <summary>
/// Provides functionality to send build notifications to a Discord channel using a webhook URL.
/// </summary>
/// <remarks>This class implements the <see cref="INotifications"/> interface to send notifications about build
/// results. It formats the notification message with details such as the branch, commit, version, commit message, and
/// build duration, and sends it to the specified Discord webhook URL.</remarks>
public class DiscordNotifications : INotifications
{
    /// <summary>How long a notification may take before it is abandoned (I44: a slow notification never holds the build).</summary>
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpMessageHandler _handler;

    private readonly Func<string> _commitMessage;

    public DiscordNotifications() : this(new HttpClientHandler(), ReadLastCommitMessage) { }

    internal DiscordNotifications(HttpMessageHandler handler, Func<string> commitMessage)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _commitMessage = commitMessage ?? throw new ArgumentNullException(nameof(commitMessage));
    }

    /// <summary>
    /// Sends a build notification to a specified webhook URL.
    /// </summary>
    /// <remarks>The method constructs a notification message based on the build status and other parameters,
    /// and sends it to the specified webhook URL. If the webhook URL is not set, the method logs a warning and returns
    /// without sending a notification.</remarks>
    /// <param name="p">The parameters for the notification, including the webhook URL, build status, branch, commit, version, and build
    /// duration.</param>
    /// <returns>A task that completes when the notification was delivered, rejected or abandoned. It never faults:
    /// a failed notification is a warning and never changes the build's outcome (I44). The webhook URL is never
    /// written to any output.</returns>
    public async Task Send(NotificationParams p)
    {
        try
        {
            await SendCore(p);
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to Send Notification: {Error}", ex.GetType().Name);
        }
    }

    private async Task SendCore(NotificationParams p)
    {
        if (string.IsNullOrWhiteSpace(p.WebHookUrl))
        {
            Log.Warning("⚠️ Build Notifications Webhook Url is Not Set.");

            return;
        }

        var title = p.BuildSucceeded ? "✅ Build Succeeded" : "❌ Build Failed";
        var color = p.BuildSucceeded ? 0x57F287 : 0xED4245;

        var formattedCommitMessage = string.Empty;
        try
        {
            formattedCommitMessage = _commitMessage();
        }
        catch (Exception ex)
        {
            Log.Warning("Could not Read the Commit Message for the Notification: {Error}", ex.Message);
        }

        var durationText = p.BuildDuration.TotalMinutes >= 1
            ? $"{p.BuildDuration.TotalMinutes:N1}m"
            : $"{p.BuildDuration.TotalSeconds:N0}s";

        var branchLink = p.Urls?.Branch != null ? $"[`{p.Branch}`]({p.Urls?.Branch})" : $"`{p.Branch}`";
        var commitLink = p.Urls?.Commit != null ? $"[`{p.Commit}`]({p.Urls?.Commit})" : $"`{p.Commit}`";

        var description =
            $"**Branch:** {branchLink}\n" +
            $"**Commit:** {commitLink}\n" +
            $"**Version:** `{p.Version}`\n" +
            $"**Message:**\n```{formattedCommitMessage}```\n" +
            $"**Duration:** `{durationText}`";

        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title,
                    description,
                    color,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    footer = new { text = "NUKE Build System" }
                }
            }
        };

        // The URL carries the webhook token: only the host is ever logged.
        var host = Uri.TryCreate(p.WebHookUrl, UriKind.Absolute, out var uri) ? uri.Host : "(invalid URL)";
        Log.Information("Sending Notification to {Host}...", host);

        using var client = new HttpClient(_handler, disposeHandler: false) { Timeout = SendTimeout };
        using var response = await client.PostAsJsonAsync(p.WebHookUrl, payload);

        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("Failed to Send Notification: {StatusCode}", (int)response.StatusCode);
        }
    }

    private static string ReadLastCommitMessage()
    {
        var lines = ProcessTasks
            .StartProcess("git", "log -1 --pretty=%B", logOutput: false, logInvocation: false)
            .AssertZeroExitCode()
            .Output
            .Select(o => o.Text.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(20);

        return string.Join("\n", lines);
    }
}
