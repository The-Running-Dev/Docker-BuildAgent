#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

using Notifications;
using Parameters;

namespace Common.Tests.Notifications;

/// <summary>
/// Unit tests for <see cref="DiscordNotifications"/>: I44 — a notification never changes the build's
/// outcome, and the webhook URL never appears in any output.
/// </summary>
[Collection(nameof(GlobalLoggerCollection))]
public class DiscordNotificationsTests : IDisposable
{
    private const string Token = "s3cr3t-webhook-token";

    private const string WebHookUrl = "https://discord.example/api/webhooks/123/" + Token;

    private readonly CapturingSink _sink = new();

    private readonly ILogger _previousLogger = Log.Logger;

    public DiscordNotificationsTests()
    {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(_sink).CreateLogger();
    }

    public void Dispose()
    {
        Log.Logger = _previousLogger;
    }

    [Fact]
    public async Task Send_LogsTheHostButNeverTheWebhookUrl()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new DiscordNotifications(handler, () => "commit message");

        await sut.Send(Params());

        Assert.Equal(1, handler.Calls);
        Assert.Contains(_sink.Messages, m => m.Contains("discord.example"));
        Assert.DoesNotContain(_sink.Messages, m => m.Contains(Token));
    }

    [Fact]
    public async Task Send_WhenTheServerRejects_WarnsWithoutEchoingTheResponseOrUrl()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent($"bad webhook {WebHookUrl}")
        });
        var sut = new DiscordNotifications(handler, () => "commit message");

        await sut.Send(Params());

        Assert.Contains(_sink.Events, e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("400"));
        Assert.DoesNotContain(_sink.Events, e => e.Level >= LogEventLevel.Error);
        Assert.DoesNotContain(_sink.Messages, m => m.Contains(Token));
    }

    [Fact]
    public async Task Send_WhenThePostThrows_CompletesWithAWarning()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException($"connection refused {WebHookUrl}"));
        var sut = new DiscordNotifications(handler, () => "commit message");

        var exception = await Record.ExceptionAsync(() => sut.Send(Params()));

        Assert.Null(exception);
        Assert.Contains(_sink.Events, e => e.Level == LogEventLevel.Warning);
        Assert.DoesNotContain(_sink.Messages, m => m.Contains(Token));
    }

    [Fact]
    public async Task Send_WhenThePostTimesOut_CompletesWithAWarning()
    {
        var handler = new StubHandler(_ => throw new TaskCanceledException("timed out"));
        var sut = new DiscordNotifications(handler, () => "commit message");

        var exception = await Record.ExceptionAsync(() => sut.Send(Params()));

        Assert.Null(exception);
        Assert.Contains(_sink.Events, e => e.Level == LogEventLevel.Warning);
    }

    [Fact]
    public async Task Send_WhenTheCommitMessageCannotBeRead_StillPosts()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new DiscordNotifications(handler, () => throw new InvalidOperationException("not a git repository"));

        var exception = await Record.ExceptionAsync(() => sut.Send(Params()));

        Assert.Null(exception);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void SendTimeout_IsBounded()
    {
        Assert.InRange(DiscordNotifications.SendTimeout, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
    }

    private static NotificationParams Params() => new()
    {
        BuildSucceeded = true,
        Branch = "main",
        Commit = "abc1234",
        Version = "1.2.3",
        BuildDuration = TimeSpan.FromSeconds(5),
        WebHookUrl = WebHookUrl
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(respond(request));
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = [];

        public IReadOnlyList<LogEvent> Events
        {
            get { lock (_events) { return [.. _events]; } }
        }

        public IEnumerable<string> Messages
        {
            get
            {
                foreach (var e in Events)
                {
                    yield return e.RenderMessage() + " " + e.Exception;
                }
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_events) { _events.Add(logEvent); }
        }
    }
}

/// <summary>Serialises the test classes that replace the global Serilog logger.</summary>
[CollectionDefinition(nameof(GlobalLoggerCollection), DisableParallelization = true)]
public class GlobalLoggerCollection;
