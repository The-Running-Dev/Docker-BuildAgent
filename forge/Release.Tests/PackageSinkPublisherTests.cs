#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Release.Tests.TestSupport;

using Xunit;

namespace Release.Tests;

public sealed class PackageSinkPublisherTests : IDisposable
{
    private const string ApiKey = "secret-api-key";
    private static readonly ReleaseVersion Version = ReleaseVersion.Parse("2.1.0-RC1");

    private readonly string _work = Path.Combine(Path.GetTempPath(), "package-sink-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _package;

    public PackageSinkPublisherTests()
    {
        Directory.CreateDirectory(_work);
        _package = Path.Combine(_work, "BuildAgent.Tool.2.1.0-RC1.nupkg");
        File.WriteAllBytes(_package, Zip(("BuildAgent.Tool.nuspec", "<package />"), ("tools/net8.0/any/Tool.dll", "binary")));
    }

    public void Dispose() => Directory.Delete(_work, recursive: true);

    [Fact]
    public void BuiltIdentity_IsThePackageContentHash()
    {
        Assert.Equal(PackageContentHash.Compute(_package), Publisher(new FakeCommandRunner(), Respond(HttpStatusCode.NotFound)).BuiltIdentity);
    }

    [Fact]
    public async Task FindPublished_HashesTheFeedsCopy_AndIgnoresTheFeedsSignature()
    {
        var signed = Zip(("BuildAgent.Tool.nuspec", "<package />"), ("tools/net8.0/any/Tool.dll", "binary"), (".signature.p7s", "signature"));
        var http = Respond(HttpStatusCode.OK, signed);
        var publisher = Publisher(new FakeCommandRunner(), http);

        Assert.Equal(publisher.BuiltIdentity, await publisher.FindPublishedIdentityAsync(Version));
        Assert.Equal(
            "https://api.nuget.org/v3-flatcontainer/buildagent.tool/2.1.0-rc1/buildagent.tool.2.1.0-rc1.nupkg",
            http.Requested.Single().ToString());
    }

    [Fact]
    public async Task FindPublished_ReportsADifferentPackage()
    {
        var publisher = Publisher(new FakeCommandRunner(), Respond(HttpStatusCode.OK, Zip(("BuildAgent.Tool.nuspec", "<other />"))));

        Assert.NotEqual(publisher.BuiltIdentity, await publisher.FindPublishedIdentityAsync(Version));
    }

    [Fact]
    public async Task FindPublished_IsNull_WhenTheFeedDoesNotHaveTheVersion()
    {
        Assert.Null(await Publisher(new FakeCommandRunner(), Respond(HttpStatusCode.NotFound)).FindPublishedIdentityAsync(Version));
    }

    [Fact]
    public async Task FindPublished_Throws_WhenTheFeedCannotBeRead()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Publisher(new FakeCommandRunner(), Respond(HttpStatusCode.ServiceUnavailable)).FindPublishedIdentityAsync(Version));
        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public async Task Publish_PushesThePackageToTheFeed()
    {
        var runner = new FakeCommandRunner();

        await Publisher(runner, Respond(HttpStatusCode.NotFound)).PublishAsync(ImageSinkPublishersTests.Claim(Version));

        var (fileName, arguments) = runner.Calls.Single();
        Assert.Equal("dotnet", fileName);
        Assert.Equal(new[] { "nuget", "push", _package, "--source", "https://api.nuget.org/v3/index.json", "--api-key", ApiKey }, arguments);
    }

    [Fact]
    public async Task Publish_ExplainsAPushTheFeedAlreadyHas()
    {
        var runner = new FakeCommandRunner().On(new[] { "nuget", "push" }, FakeCommandRunner.Fail("Response status code does not indicate success: 409 (Conflict)."));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Publisher(runner, Respond(HttpStatusCode.NotFound)).PublishAsync(ImageSinkPublishersTests.Claim(Version)));
        Assert.Contains("does not serve it yet", ex.Message);
    }

    [Fact]
    public async Task Publish_Failure_NeverCarriesTheApiKey()
    {
        var runner = new FakeCommandRunner().On(new[] { "nuget", "push" }, FakeCommandRunner.Fail("403 (Forbidden)"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Publisher(runner, Respond(HttpStatusCode.NotFound)).PublishAsync(ImageSinkPublishersTests.Claim(Version)));
        Assert.Contains("403", ex.Message);
        Assert.DoesNotContain(ApiKey, ex.Message);
    }

    [Fact]
    public async Task Publish_Refuses_AClaimForAnotherVersion()
    {
        var runner = new FakeCommandRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Publisher(runner, Respond(HttpStatusCode.NotFound)).PublishAsync(ImageSinkPublishersTests.Claim(ReleaseVersion.Parse("2.1.0"))));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void Constructor_Refuses_AMissingApiKey()
    {
        Assert.Throws<ArgumentException>(() => new PackageSinkPublisher(
            ReleaseSink.GlobalTool, _package, "BuildAgent.Tool", Version, PackageFeed.NuGetOrg, " ", new FakeCommandRunner(), new HttpClient()));
    }

    [Fact]
    public void PowerShellGallery_DownloadsByIdAndVersion()
    {
        Assert.Equal(
            "https://www.powershellgallery.com/api/v2/package/Docker-BuildAgent/2.1.0-rc1",
            PackageFeed.PowerShellGallery.DownloadUri("Docker-BuildAgent", "2.1.0-rc1").ToString());
    }

    private PackageSinkPublisher Publisher(FakeCommandRunner runner, RecordingHandler http) =>
        new(ReleaseSink.GlobalTool, _package, "BuildAgent.Tool", Version, PackageFeed.NuGetOrg, ApiKey, runner, new HttpClient(http));

    private static RecordingHandler Respond(HttpStatusCode status, byte[]? body = null) => new(status, body);

    private static byte[] Zip(params (string Path, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                using var entry = archive.CreateEntry(path).Open();
                entry.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        return stream.ToArray();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly byte[]? _body;

        public RecordingHandler(HttpStatusCode status, byte[]? body)
        {
            _status = status;
            _body = body;
        }

        public List<Uri> Requested { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requested.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new ByteArrayContent(_body ?? Array.Empty<byte>()) });
        }
    }
}
