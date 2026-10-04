#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Update;
using Xunit;

namespace Update.Tests;

// design/10-design.md § Control flow 3 step 13, § Failure modes "Notification"; design/20-contract.md § Notifications,
// invariant I44. Console output is process-wide: these tests capture it, so they share a collection with the other
// tests that do.
[Collection("ConsoleOutput")]
public sealed class UpdateNotificationTests : IDisposable
{
    // Carries a token and a query string, both of which must stay out of every output.
    private const string HookUrl = "https://hooks.example.com/api/webhooks/123456/SECRETTOKEN?thread_id=SECRETQUERY";

    private static readonly string[] Secrets = { HookUrl, "SECRETTOKEN", "SECRETQUERY", "hooks.example.com" };

    private readonly string _logPath;
    private readonly UpdateLogStore _log;
    private readonly FakeDockerRuntime _runtime = new();
    private readonly FakeUpdateClock _clock = new();
    private readonly UpdaterIdentity _identity = new("test-host", 4321);

    public UpdateNotificationTests()
    {
        _logPath = Path.Combine(Path.GetTempPath(), $"updates-{Guid.NewGuid():N}.jsonl");
        _log = new UpdateLogStore(_logPath);
    }

    public void Dispose()
    {
        if (File.Exists(_logPath))
        {
            File.Delete(_logPath);
        }
    }

    private static readonly UpdateOptions Options = new(TimeSpan.FromSeconds(2), true);

    private static ContainerInspection Target(string name, string imageId, string imageReference) => new(
        Id: Guid.NewGuid().ToString("N"),
        Name: name,
        ImageId: imageId,
        ImageReference: imageReference,
        Running: true,
        AutoRemove: false,
        Labels: new Dictionary<string, string> { ["com.example.owner"] = "team-a" },
        Env: new[] { "FOO=bar" },
        Command: Array.Empty<string>(),
        Entrypoint: Array.Empty<string>(),
        Mounts: Array.Empty<MountSpec>(),
        Ports: Array.Empty<PortSpec>(),
        RestartPolicy: "unless-stopped",
        Networks: new[] { "bridge" },
        Links: Array.Empty<string>());

    /// <summary>Seeds "web" at 1.0 with 2.0 available. Healthy by default; <paramref name="healthy"/> false leaves the
    /// replacement stuck "starting", so the update times out and is restored.</summary>
    private void SeedWeb(bool healthy)
    {
        var target = Target("web", "sha256:old", "web:1.0");
        _runtime.SeedContainer(target);
        _runtime.SeedImage("web:1.0", "sha256:old", local: true);
        _runtime.SeedImage("web:2.0", "sha256:new", local: true);
        if (!healthy)
        {
            _runtime.HealthProbe = name => name == "web" ? (true, "starting") : null;
        }
    }

    private Updater UpdaterWith(IUpdateNotifier notifier) =>
        new(_runtime, _log, _clock, _identity, notifier);

    /// <summary>Runs <paramref name="run"/> with stdout and stderr captured, and returns what it wrote to both.</summary>
    private static async Task<(T Result, string Output)> CaptureAsync<T>(Func<Task<T>> run)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var captured = new StringWriter();
        Console.SetOut(captured);
        Console.SetError(captured);
        try
        {
            var result = await run();
            return (result, captured.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static void AssertNoSecret(string output)
    {
        foreach (var secret in Secrets)
        {
            Assert.DoesNotContain(secret, output, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class RecordingNotifier : IUpdateNotifier
    {
        private readonly Func<string?, UpdateNotification, Task<UpdateNotificationResult>> _send;

        public RecordingNotifier(Func<string?, UpdateNotification, Task<UpdateNotificationResult>>? send = null)
        {
            _send = send ?? ((_, _) => Task.FromResult(UpdateNotificationResult.Success));
        }

        public List<(string? Url, UpdateNotification Notification)> Calls { get; } = new();

        public Task<UpdateNotificationResult> SendAsync(string? webhookUrl, UpdateNotification notification)
        {
            Calls.Add((webhookUrl, notification));
            return _send(webhookUrl, notification);
        }
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handle;

        public DelegateHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) =>
            _handle = handle;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handle(request, cancellationToken);
    }

    // Control flow 3 step 13: "sent after the outcome is logged".
    [Fact]
    public async Task Notification_IsSentAfterTheOutcomeIsLogged()
    {
        SeedWeb(healthy: true);
        string[]? logAtSend = null;
        bool? lockHeldAtSend = null;
        var notifier = new RecordingNotifier((_, _) =>
        {
            logAtSend = File.ReadAllLines(_logPath);
            lockHeldAtSend = _runtime.Get(UpdateNaming.LockContainerName("web")) != null;
            return Task.FromResult(UpdateNotificationResult.Success);
        });

        var outcome = await UpdaterWith(notifier).RunAsync("web", "web:2.0", Options, HookUrl);

        Assert.Equal(UpdateOutcome.Succeeded, outcome);
        var call = Assert.Single(notifier.Calls);
        Assert.Equal(HookUrl, call.Url);
        Assert.Equal(new UpdateNotification("web", UpdateOutcome.Succeeded), call.Notification);

        Assert.NotNull(logAtSend);
        Assert.Equal(2, logAtSend!.Length);
        using var completed = JsonDocument.Parse(logAtSend[1]);
        Assert.Equal("succeeded", completed.RootElement.GetProperty("outcome").GetString());
        Assert.False(lockHeldAtSend);
    }

    [Fact]
    public async Task Notification_ReportsTheOutcomeOfAFailedUpdateToo()
    {
        SeedWeb(healthy: false);
        var notifier = new RecordingNotifier();

        var outcome = await UpdaterWith(notifier).RunAsync("web", "web:2.0", Options, HookUrl);

        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, outcome);
        Assert.Equal(UpdateOutcome.RestoredAfterUnhealthy, Assert.Single(notifier.Calls).Notification.Outcome);
    }

    [Fact]
    public async Task Notification_IsNotSent_WhenNoneWasRequested()
    {
        SeedWeb(healthy: true);
        var notifier = new RecordingNotifier();

        var (outcome, output) = await CaptureAsync(() => UpdaterWith(notifier).RunAsync("web", "web:2.0", Options));

        Assert.Equal(UpdateOutcome.Succeeded, outcome);
        Assert.Empty(notifier.Calls);
        Assert.DoesNotContain("notification", output, StringComparison.OrdinalIgnoreCase);
    }

    // A refusal throws before any outcome exists, so there is nothing to notify about.
    [Fact]
    public async Task Notification_IsNotSent_ForARefusal()
    {
        var notifier = new RecordingNotifier();

        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => UpdaterWith(notifier).RunAsync("ghost", null, Options, HookUrl));

        Assert.Equal(UpdateErrorCode.TargetNotFound, ex.Code);
        Assert.Empty(notifier.Calls);
    }

    // I44: a failed notification does not change the exit status, for a success and for a failure outcome alike,
    // and however it fails: a reported failure, or an exception whose message names the URL.
    [Theory]
    [InlineData(true, UpdateOutcome.Succeeded, 0, "result")]
    [InlineData(true, UpdateOutcome.Succeeded, 0, "throws")]
    [InlineData(false, UpdateOutcome.RestoredAfterUnhealthy, 10, "result")]
    [InlineData(false, UpdateOutcome.RestoredAfterUnhealthy, 10, "throws")]
    public async Task FailedNotification_DoesNotChangeTheExitStatus(
        bool healthy, UpdateOutcome expectedOutcome, int expectedExitCode, string how)
    {
        SeedWeb(healthy);
        var notifier = new RecordingNotifier((_, _) => how == "result"
            ? Task.FromResult(UpdateNotificationResult.DeliveryFailed("The webhook answered with status 500."))
            : throw new HttpRequestException($"Connection to {HookUrl} refused"));

        var (outcome, output) = await CaptureAsync(() => UpdaterWith(notifier).RunAsync("web", "web:2.0", Options, HookUrl));

        Assert.Equal(expectedOutcome, outcome);
        Assert.Equal(expectedExitCode, UpdateExitCode.ForOutcome(outcome));
        Assert.Contains("DeliveryFailed", output);
        AssertNoSecret(output);
    }

    // I44: a slow notification does not change the exit status, and does not hang the command.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SlowWebhook_TimesOut_WithoutChangingTheExitStatus(bool handlerHonoursCancellation)
    {
        SeedWeb(healthy: true);
        var handler = new DelegateHandler(async (_, token) =>
        {
            // The second handler ignores the token, as a misbehaving transport might.
            await Task.Delay(TimeSpan.FromMinutes(5), handlerHonoursCancellation ? token : CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var notifier = new WebhookUpdateNotifier(handler, timeout: TimeSpan.FromMilliseconds(100));

        var started = DateTime.UtcNow;
        var (outcome, output) = await CaptureAsync(() => UpdaterWith(notifier).RunAsync("web", "web:2.0", Options, HookUrl));

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(30));
        Assert.Equal(UpdateOutcome.Succeeded, outcome);
        Assert.Equal(0, UpdateExitCode.ForOutcome(outcome));
        Assert.Contains("DeliveryFailed", output);
        Assert.Contains("did not answer", output);
        AssertNoSecret(output);
    }

    // I44: the URL, its token and its query string never appear in any output, on any path.
    public static IEnumerable<object[]> DeliveryPaths() => new[]
    {
        new object[] { "status", true },
        new object[] { "transport-exception", true },
        new object[] { "timeout", true },
        new object[] { "success", false },
    };

    [Theory]
    [MemberData(nameof(DeliveryPaths))]
    public async Task TheWebhookUrl_NeverAppearsInAnyOutput(string path, bool expectWarning)
    {
        SeedWeb(healthy: true);
        var handler = new DelegateHandler((request, token) => path switch
        {
            "status" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent($"error at {request.RequestUri}"),
            }),
            "transport-exception" => throw new HttpRequestException(
                $"No such host is known ({request.RequestUri}).", new IOException(request.RequestUri!.ToString())),
            "timeout" => Task.Delay(TimeSpan.FromMinutes(5), token).ContinueWith(
                _ => new HttpResponseMessage(HttpStatusCode.OK), token),
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)),
        });
        var notifier = new WebhookUpdateNotifier(handler, timeout: TimeSpan.FromMilliseconds(100));

        var (outcome, output) = await CaptureAsync(() => UpdaterWith(notifier).RunAsync("web", "web:2.0", Options, HookUrl));

        Assert.Equal(UpdateOutcome.Succeeded, outcome);
        Assert.Equal(expectWarning, output.Contains("update notification was not sent", StringComparison.Ordinal));
        AssertNoSecret(output);
    }

    [Theory]
    [InlineData("ftp://hooks.example.com/SECRETTOKEN?thread_id=SECRETQUERY")]
    [InlineData("hooks.example.com/SECRETTOKEN?thread_id=SECRETQUERY")]
    public async Task AnAddressThatIsNotAnHttpUrl_IsAWarning_WithoutTheAddress(string address)
    {
        SeedWeb(healthy: true);
        var notifier = new WebhookUpdateNotifier(new DelegateHandler((_, _) =>
            throw new InvalidOperationException("must not be reached")));

        var (outcome, output) = await CaptureAsync(() => UpdaterWith(notifier).RunAsync("web", "web:2.0", Options, address));

        Assert.Equal(UpdateOutcome.Succeeded, outcome);
        Assert.Contains("DeliveryFailed", output);
        AssertNoSecret(output);
    }

    // Contract § Notifications, NotConfigured: requested with no URL, warn, exit status unchanged.
    [Theory]
    [InlineData("", true, UpdateOutcome.Succeeded)]
    [InlineData("   ", true, UpdateOutcome.Succeeded)]
    [InlineData("", false, UpdateOutcome.RestoredAfterUnhealthy)]
    public async Task NotificationRequestedWithNoUrl_Warns_AndLeavesTheExitStatus(
        string notifyUrl, bool healthy, UpdateOutcome expectedOutcome)
    {
        SeedWeb(healthy);
        var notifier = new RecordingNotifier();

        var (outcome, output) = await CaptureAsync(() => UpdaterWith(notifier).RunAsync("web", "web:2.0", Options, notifyUrl));

        Assert.Equal(expectedOutcome, outcome);
        Assert.Contains("NotConfigured", output);
        Assert.Contains("no webhook URL", output);
        Assert.Empty(notifier.Calls);
    }

    [Fact]
    public async Task TheSender_AnswersNotConfigured_ForABlankUrl()
    {
        var notifier = new WebhookUpdateNotifier(new DelegateHandler((_, _) =>
            throw new InvalidOperationException("must not be reached")));

        var result = await notifier.SendAsync(null, new UpdateNotification("web", UpdateOutcome.Succeeded));

        Assert.False(result.Delivered);
        Assert.Equal(UpdateNotificationErrorCode.NotConfigured, result.Error!.Code);
    }

    [Fact]
    public async Task TheSender_PostsADiscordStyleEmbed_ToTheUrl()
    {
        HttpRequestMessage? seen = null;
        string? body = null;
        var handler = new DelegateHandler(async (request, _) =>
        {
            seen = request;
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var notifier = new WebhookUpdateNotifier(handler);

        var result = await notifier.SendAsync(HookUrl, new UpdateNotification("web", UpdateOutcome.RestoredAfterUnhealthy));

        Assert.True(result.Delivered);
        Assert.Equal(HttpMethod.Post, seen!.Method);
        Assert.Equal(HookUrl, seen.RequestUri!.ToString());
        Assert.Equal("application/json", seen.Content!.Headers.ContentType!.MediaType);

        using var document = JsonDocument.Parse(body!);
        var embed = document.RootElement.GetProperty("embeds").EnumerateArray().Single();
        Assert.Contains("restored", embed.GetProperty("title").GetString());
        Assert.Contains("web", embed.GetProperty("description").GetString());
        Assert.Contains("RestoredAfterUnhealthy", embed.GetProperty("description").GetString());
        Assert.Equal(0xED4245, embed.GetProperty("color").GetInt32());
        Assert.True(embed.TryGetProperty("timestamp", out _));
        Assert.True(embed.TryGetProperty("footer", out _));
    }

    // I44 reaches the command line too: a URL on a rejected argument is not echoed back.
    [Fact]
    public void CommandLine_DoesNotEchoTheUrl_OfARejectedNotifyArgument()
    {
        var equalsForm = Assert.Throws<UpdateCommandLineException>(
            () => UpdateCommandLine.Parse(new[] { "web", $"--notify={HookUrl}" }));
        AssertNoSecret(equalsForm.Message);

        var strayUrl = Assert.Throws<UpdateCommandLineException>(
            () => UpdateCommandLine.Parse(new[] { "web", "--image", "--notify", HookUrl }));
        AssertNoSecret(strayUrl.Message);
    }
}
