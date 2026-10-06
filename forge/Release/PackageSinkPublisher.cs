#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Release;

/// <summary>A package feed: where a package is pushed, and where one version of it is downloaded.</summary>
public sealed record PackageFeed(string Name, string PushSource, Func<string, string, Uri> DownloadUri)
{
    /// <summary>nuget.org, which serves the global tool. Its flat container takes lower-case ids and versions.</summary>
    public static PackageFeed NuGetOrg { get; } = new(
        "nuget.org",
        "https://api.nuget.org/v3/index.json",
        (id, version) =>
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = version.ToLowerInvariant();
            return new Uri($"https://api.nuget.org/v3-flatcontainer/{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg");
        });

    /// <summary>The PowerShell Gallery, which serves the module.</summary>
    public static PackageFeed PowerShellGallery { get; } = new(
        "the PowerShell Gallery",
        "https://www.powershellgallery.com/api/v2/package",
        (id, version) => new Uri($"https://www.powershellgallery.com/api/v2/package/{id}/{version}"));
}

/// <summary>
/// A sink that is a package on a feed: the global tool on nuget.org, or the module on the
/// PowerShell Gallery. Its identity is the package's <see cref="PackageContentHash"/>, which the
/// feed's own copy keeps even when the feed signs it on upload.
/// </summary>
public sealed class PackageSinkPublisher : IVersionedSinkPublisher
{
    private readonly string _packagePath;
    private readonly string _packageId;
    private readonly ReleaseVersion _version;
    private readonly PackageFeed _feed;
    private readonly string _apiKey;
    private readonly ICommandRunner _runner;
    private readonly HttpClient _http;

    public PackageSinkPublisher(
        ReleaseSink sink,
        string packagePath,
        string packageId,
        ReleaseVersion version,
        PackageFeed feed,
        string apiKey,
        ICommandRunner runner,
        HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException($"No API key for {feed.Name}.", nameof(apiKey));
        }

        Sink = sink;
        _packagePath = packagePath;
        _packageId = packageId;
        _version = version;
        _feed = feed;
        _apiKey = apiKey;
        _runner = runner;
        _http = http;
        BuiltIdentity = PackageContentHash.Compute(packagePath);
    }

    public ReleaseSink Sink { get; }

    public string BuiltIdentity { get; }

    /// <summary>
    /// Pushes the package. A push the feed refuses because it has the version already (a push of
    /// an earlier attempt the feed has not yet started serving) fails with that explanation, so
    /// the operator re-runs once the feed serves it and the comparison can see it.
    /// </summary>
    public async Task PublishAsync(ReleaseClaim claim)
    {
        ImageVersionedTagPublisher.EnsureBuiltFor(claim.Version, _version);

        var arguments = new[] { "nuget", "push", _packagePath, "--source", _feed.PushSource, "--api-key", _apiKey };
        var result = await _runner.RunAsync("dotnet", arguments).ConfigureAwait(false);
        if (result.Succeeded)
        {
            return;
        }

        var output = result.Combined;
        if (output.Contains("409", StringComparison.Ordinal) || output.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{_feed.Name} already has {_packageId} {_version.ToPackageString()} but does not serve it yet; re-run the release once it does.");
        }

        throw new InvalidOperationException(
            $"{CommandRunnerExtensions.Describe("dotnet", arguments)} to {_feed.Name} exited {result.ExitCode}: {result.Error.Trim()} {result.Output.Trim()}".TrimEnd());
    }

    /// <summary>
    /// Downloads the feed's copy of the version and hashes it. A feed answering 404 does not hold
    /// the version; any other failure throws (I5).
    /// </summary>
    public async Task<string?> FindPublishedIdentityAsync(ReleaseVersion version)
    {
        var uri = _feed.DownloadUri(_packageId, version.ToPackageString());
        using var response = await _http.GetAsync(uri).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Could not read {_packageId} {version.ToPackageString()} from {_feed.Name}: HTTP {(int)response.StatusCode}.");
        }

        using var package = new MemoryStream();
        await response.Content.CopyToAsync(package).ConfigureAwait(false);
        package.Position = 0;
        return PackageContentHash.Compute(package);
    }
}
