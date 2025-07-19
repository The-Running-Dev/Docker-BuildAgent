using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using Serilog;
using Nuke.Common.Tooling;

using Utilities;
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
    /// <summary>
    /// Sends a build notification to a specified webhook URL.
    /// </summary>
    /// <remarks>The method constructs a notification message based on the build status and other parameters,
    /// and sends it to the specified webhook URL. If the webhook URL is not set, the method logs a warning and returns
    /// without sending a notification.</remarks>
    /// <param name="p">The parameters for the notification, including the webhook URL, build status, branch, commit, version, and build
    /// duration.</param>
    /// <returns></returns>
    public async Task Send(NotificationParams p)
    {
        if (string.IsNullOrWhiteSpace(p.WebHookUrl))
        {
            Log.Warning("⚠️ Build Notifications Webhook Url is Not Set.");

            return;
        }

        var title = p.BuildSucceeded ? "✅ Build Succeeded" : "❌ Build Failed";
        var color = p.BuildSucceeded ? 0x57F287 : 0xED4245;

        var commitMessage = ProcessTasks
            .StartProcess("git", "log -1 --pretty=%B", logOutput: false, logInvocation: false)
            .AssertZeroExitCode()
            .Output
            .Select(o => o.Text.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(20)
            .ToList();

        var formattedCommitMessage = string.Join("\n", commitMessage);
        var durationText = p.BuildDuration.TotalMinutes >= 1
            ? $"{p.BuildDuration.TotalMinutes:N1}m"
            : $"{p.BuildDuration.TotalSeconds:N0}s";

        var (repoUrl, branchUrl, commitUrl) = Git.Urls(p.Branch, p.Commit);
        var branchLink = branchUrl != null ? $"[`{p.Branch}`]({branchUrl})" : $"`{p.Branch}`";
        var commitLink = commitUrl != null ? $"[`{p.Commit}`]({commitUrl})" : $"`{p.Commit}`";

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

        Log.Information($"Sending Notification...{p.WebHookUrl.Substring(10)}");

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(p.WebHookUrl, payload);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            Log.Error($"❌ Failed to Send Notification: {response.StatusCode}\n{error}");
        }
    }
}
