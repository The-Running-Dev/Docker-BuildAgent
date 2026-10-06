#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Release.Tests.TestSupport;

using Xunit;

namespace Release.Tests;

public sealed class ImageSinkPublishersTests : IDisposable
{
    private const string Image = "ghcr.io/owner/build-agent";
    private const string ConfigDigest = "sha256:c0ffee";
    private static readonly string[] Inspect = { "buildx", "imagetools", "inspect", "--raw" };
    private static readonly ReleaseVersion Version = ReleaseVersion.Parse("2.1.0-rc1");

    private readonly string _work = Path.Combine(Path.GetTempPath(), "image-sink-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    [Fact]
    public async Task Build_StampsTheVersion_AndTakesTheConfigDigestAsTheIdentity()
    {
        var runner = new FakeCommandRunner().On(
            new[] { "buildx", "build" },
            WriteMetadata("{\"containerimage.config.digest\":\"" + ConfigDigest + "\",\"containerimage.digest\":\"sha256:other\"}"));

        var publisher = await ImageVersionedTagPublisher.BuildAsync(Image, Version, "ctx", _work, runner);

        Assert.Equal(ConfigDigest, publisher.BuiltIdentity);
        var build = Assert.Single(runner.Calls).Arguments;
        Assert.Contains("IMAGE_VERSION=2.1.0-rc1", build);
        Assert.Contains("--provenance=false", build);
        Assert.Contains("--sbom=false", build);
        Assert.Equal($"{Image}:2.1.0-rc1", build[build.ToList().IndexOf("--tag") + 1]);
        Assert.Equal("ctx", build[^1]);
    }

    [Fact]
    public async Task Build_Fails_WhenTheBuildRecordsNoConfigDigest()
    {
        var runner = new FakeCommandRunner().On(new[] { "buildx", "build" }, WriteMetadata("{\"containerimage.digest\":\"sha256:other\"}"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ImageVersionedTagPublisher.BuildAsync(Image, Version, "ctx", _work, runner));
        Assert.Contains("no image config digest", ex.Message);
    }

    [Fact]
    public async Task Build_Fails_WhenDockerFails()
    {
        var runner = new FakeCommandRunner().On(new[] { "buildx", "build" }, FakeCommandRunner.Fail("failed to solve"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ImageVersionedTagPublisher.BuildAsync(Image, Version, "ctx", _work, runner));
        Assert.Contains("failed to solve", ex.Message);
    }

    [Fact]
    public async Task FindPublished_ReadsTheConfigDigestOfAnImageManifest()
    {
        var runner = new FakeCommandRunner().On(Inspect, FakeCommandRunner.Ok(Manifest(ConfigDigest)));

        Assert.Equal(ConfigDigest, await Publisher(runner).FindPublishedIdentityAsync(Version));
        Assert.Equal($"{Image}:2.1.0-rc1", runner.Calls.Single().Arguments[^1]);
    }

    [Fact]
    public async Task FindPublished_ReadsTheLinuxAmd64ImageOfAnIndex()
    {
        const string index = "{\"mediaType\":\"application/vnd.oci.image.index.v1+json\",\"manifests\":["
            + "{\"digest\":\"sha256:arm\",\"platform\":{\"os\":\"linux\",\"architecture\":\"arm64\"}},"
            + "{\"digest\":\"sha256:amd\",\"platform\":{\"os\":\"linux\",\"architecture\":\"amd64\"}}]}";
        var runner = new FakeCommandRunner().On(Inspect, args => args[^1] switch
        {
            $"{Image}:2.1.0-rc1" => FakeCommandRunner.Ok(index),
            $"{Image}@sha256:amd" => FakeCommandRunner.Ok(Manifest(ConfigDigest)),
            _ => FakeCommandRunner.Ok(Manifest("sha256:wrong")),
        });

        Assert.Equal(ConfigDigest, await Publisher(runner).FindPublishedIdentityAsync(Version));
    }

    [Theory]
    [InlineData("ERROR: ghcr.io/owner/build-agent:2.1.0-rc1: not found")]
    [InlineData("manifest unknown")]
    public async Task FindPublished_IsNull_WhenTheRegistryDoesNotHaveTheTag(string answer)
    {
        var runner = new FakeCommandRunner().On(Inspect, FakeCommandRunner.Fail(answer));

        Assert.Null(await Publisher(runner).FindPublishedIdentityAsync(Version));
    }

    [Fact]
    public async Task FindPublished_Throws_WhenTheRegistryCannotBeRead()
    {
        var runner = new FakeCommandRunner().On(Inspect, FakeCommandRunner.Fail("unauthorized: authentication required"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Publisher(runner).FindPublishedIdentityAsync(Version));
        Assert.Contains("unauthorized", ex.Message);
    }

    [Fact]
    public async Task Publish_PushesTheVersionedTag()
    {
        var runner = new FakeCommandRunner();

        await Publisher(runner).PublishAsync(Claim(Version));

        Assert.Equal(new[] { "push", $"{Image}:2.1.0-rc1" }, runner.Calls.Single().Arguments);
    }

    [Fact]
    public async Task Publish_Refuses_AClaimForAnotherVersion()
    {
        var runner = new FakeCommandRunner();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Publisher(runner).PublishAsync(Claim(ReleaseVersion.Parse("2.1.0"))));
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Latest_PointsLatestAtTheReleasedImage_RegistrySide()
    {
        var runner = new FakeCommandRunner();

        await new ImageLatestTagPublisher(Image, runner).PublishAsync(Claim(ReleaseVersion.Parse("2.1.0")));

        Assert.Equal(
            new[] { "buildx", "imagetools", "create", "--tag", $"{Image}:latest", $"{Image}:2.1.0" },
            runner.Calls.Single().Arguments);
    }

    [Fact]
    public async Task Latest_Fails_WhenTheRegistryRefuses()
    {
        var runner = new FakeCommandRunner().On(new[] { "buildx", "imagetools", "create" }, FakeCommandRunner.Fail("denied"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ImageLatestTagPublisher(Image, runner).PublishAsync(Claim(Version)));
    }

    private static ImageVersionedTagPublisher Publisher(FakeCommandRunner runner) => new(Image, Version, ConfigDigest, runner);

    internal static ReleaseClaim Claim(ReleaseVersion version) =>
        new(version, "abc", ClaimState.Draft, "notes", new Surface.SurfaceManifest(1, version.ToPackageString(), Array.Empty<Surface.SurfaceItem>()));

    private static string Manifest(string configDigest) =>
        "{\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"config\":{\"digest\":\"" + configDigest + "\"},\"layers\":[]}";

    private static Func<IReadOnlyList<string>, CommandResult> WriteMetadata(string json) => args =>
    {
        File.WriteAllText(args[args.ToList().IndexOf("--metadata-file") + 1], json);
        return FakeCommandRunner.Ok();
    };
}
