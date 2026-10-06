#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Release;

/// <summary>
/// The image's versioned tag (<see cref="ReleaseSink.ImageVersionedTag"/>). Its identity is the
/// image's config digest: the digest of the image content, which a push carries unchanged to the
/// registry, so the registry's answer can be compared with the build's.
/// </summary>
public sealed class ImageVersionedTagPublisher : IVersionedSinkPublisher
{
    private const string LinuxAmd64Os = "linux";
    private const string LinuxAmd64Architecture = "amd64";

    private readonly string _image;
    private readonly ReleaseVersion _version;
    private readonly ICommandRunner _runner;

    public ImageVersionedTagPublisher(string image, ReleaseVersion version, string builtIdentity, ICommandRunner runner)
    {
        _image = image;
        _version = version;
        BuiltIdentity = builtIdentity;
        _runner = runner;
    }

    public ReleaseSink Sink => ReleaseSink.ImageVersionedTag;

    public string BuiltIdentity { get; }

    /// <summary>
    /// Builds the image into the local Docker store as <c>image:version</c>, with the version
    /// stamped through the <c>IMAGE_VERSION</c> build argument, and returns its publisher. Provenance
    /// and SBOM attestations are off, so the push writes one image manifest and not an index.
    /// </summary>
    public static async Task<ImageVersionedTagPublisher> BuildAsync(
        string image, ReleaseVersion version, string contextDirectory, string workDirectory, ICommandRunner runner)
    {
        Directory.CreateDirectory(workDirectory);
        var metadataFile = Path.Combine(workDirectory, "image-metadata.json");
        var reference = Reference(image, version);

        await runner.RunCheckedAsync("docker", new[]
        {
            "buildx", "build", "--load", "--provenance=false", "--sbom=false",
            "--metadata-file", metadataFile,
            "--build-arg", $"IMAGE_VERSION={version.ToPackageString()}",
            "--tag", reference,
            contextDirectory,
        }).ConfigureAwait(false);

        using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(metadataFile).ConfigureAwait(false));
        if (!metadata.RootElement.TryGetProperty("containerimage.config.digest", out var digest)
            || string.IsNullOrWhiteSpace(digest.GetString()))
        {
            throw new InvalidOperationException($"The build of {reference} recorded no image config digest.");
        }

        return new ImageVersionedTagPublisher(image, version, digest.GetString()!, runner);
    }

    public async Task PublishAsync(ReleaseClaim claim)
    {
        EnsureBuiltFor(claim.Version, _version);
        await _runner.RunCheckedAsync("docker", new[] { "push", Reference(_image, _version) }).ConfigureAwait(false);
    }

    /// <summary>
    /// The config digest of the image the registry holds under the version, read without a pull.
    /// A tag the registry does not have is null; any other failure throws (I5).
    /// </summary>
    public async Task<string?> FindPublishedIdentityAsync(ReleaseVersion version)
    {
        var reference = Reference(_image, version);
        var manifest = await InspectRawAsync(reference).ConfigureAwait(false);
        if (manifest == null)
        {
            return null;
        }

        using (manifest)
        {
            var root = manifest.RootElement;
            if (root.TryGetProperty("manifests", out var manifests))
            {
                var platformDigest = manifests.EnumerateArray()
                    .Where(IsLinuxAmd64)
                    .Select(entry => entry.GetProperty("digest").GetString())
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException($"{reference} is an index with no linux/amd64 image.");

                using var platformManifest = await InspectRawAsync($"{_image}@{platformDigest}").ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"{reference} names image {platformDigest}, which the registry does not have.");
                return ConfigDigest(platformManifest.RootElement, reference);
            }

            return ConfigDigest(root, reference);
        }
    }

    internal static string Reference(string image, ReleaseVersion version) => $"{image}:{version.ToPackageString()}";

    internal static void EnsureBuiltFor(ReleaseVersion claimed, ReleaseVersion built)
    {
        if (claimed != built)
        {
            throw new InvalidOperationException($"This run built {built.ToPackageString()}, not the claimed {claimed.ToPackageString()}.");
        }
    }

    private async Task<JsonDocument?> InspectRawAsync(string reference)
    {
        var result = await _runner.RunAsync("docker", new[] { "buildx", "imagetools", "inspect", "--raw", reference }).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return JsonDocument.Parse(result.Output);
        }

        if (RegistryAnswers.IsNotFound(result))
        {
            return null;
        }

        throw new InvalidOperationException(
            $"Could not read {reference} from the registry (docker exit {result.ExitCode}): {result.Error.Trim()}");
    }

    private static bool IsLinuxAmd64(JsonElement entry) =>
        entry.TryGetProperty("platform", out var platform)
        && platform.TryGetProperty("os", out var os) && os.GetString() == LinuxAmd64Os
        && platform.TryGetProperty("architecture", out var architecture) && architecture.GetString() == LinuxAmd64Architecture;

    private static string ConfigDigest(JsonElement manifest, string reference)
    {
        if (manifest.TryGetProperty("config", out var config)
            && config.TryGetProperty("digest", out var digest)
            && !string.IsNullOrWhiteSpace(digest.GetString()))
        {
            return digest.GetString()!;
        }

        throw new InvalidOperationException($"The registry's manifest for {reference} names no image config.");
    }
}

/// <summary>
/// Moves the image's <c>latest</c> tag (<see cref="ReleaseSink.ImageLatestTag"/>) to the released
/// version's image, registry-side, so a completion re-run on a fresh runner needs no local image.
/// </summary>
public sealed class ImageLatestTagPublisher : IReleaseSinkPublisher
{
    public const string LatestTag = "latest";

    private readonly string _image;
    private readonly ICommandRunner _runner;

    public ImageLatestTagPublisher(string image, ICommandRunner runner)
    {
        _image = image;
        _runner = runner;
    }

    public ReleaseSink Sink => ReleaseSink.ImageLatestTag;

    public Task PublishAsync(ReleaseClaim claim) =>
        _runner.RunCheckedAsync("docker", new[]
        {
            "buildx", "imagetools", "create",
            "--tag", $"{_image}:{LatestTag}",
            ImageVersionedTagPublisher.Reference(_image, claim.Version),
        });
}

/// <summary>How a registry says it does not have a tag, as opposed to failing to answer.</summary>
internal static class RegistryAnswers
{
    private static readonly string[] NotFoundMarkers = { ": not found", "manifest unknown", "no such manifest" };

    public static bool IsNotFound(CommandResult result) =>
        NotFoundMarkers.Any(marker => result.Combined.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
