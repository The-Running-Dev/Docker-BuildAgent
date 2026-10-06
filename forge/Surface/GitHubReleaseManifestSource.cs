#nullable enable

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;

using Octokit;

namespace Surface;

/// <summary>
/// Reads surface-manifest.json from the highest published stable GitHub release below the candidate (I8),
/// via Octokit for release/asset listing and a plain HttpClient for the asset bytes (Octokit does not
/// expose release asset content directly). "Highest" is SemVer precedence, not publish date: a
/// back-published patch to an older line is never the baseline for the current one. A pre-release is
/// never the baseline, so a release's breaking changes and deprecations are counted from the previous
/// stable release, not from its own release candidates.
/// </summary>
public sealed class GitHubReleaseManifestSource : IBaselineManifestSource
{
    private const string ManifestAssetName = "surface-manifest.json";

    private readonly string _owner;
    private readonly string _repo;
    private readonly string _token;
    private readonly IGitHubClient _client;
    private readonly Func<HttpClient> _httpClientFactory;

    public GitHubReleaseManifestSource(string owner, string repo, string token, IGitHubClient? client = null, Func<HttpClient>? httpClientFactory = null)
    {
        _owner = owner;
        _repo = repo;
        _token = token;
        _client = client ?? new GitHubClient(new Octokit.ProductHeaderValue("NukeBuild")) { Credentials = new Credentials(token) };
        _httpClientFactory = httpClientFactory ?? (() => new HttpClient());
    }

    public async Task<string?> GetBaselineManifestJsonAsync(string candidateVersion)
    {
        if (!SemanticVersion.TryParse(candidateVersion, out var candidate))
        {
            throw new SurfaceException(SurfaceErrorCode.BaselineUnreadable, $"'{candidateVersion}' is not a semantic version, so no baseline below it can be chosen.");
        }

        IReadOnlyList<Release> releases;
        try
        {
            releases = await _client.Repository.Release.GetAll(_owner, _repo);
        }
        catch (Exception ex)
        {
            throw new SurfaceException(SurfaceErrorCode.BaselineUnreadable, $"Could not list releases for {_owner}/{_repo}: {ex.Message}", ex);
        }

        var baselineTag = SelectBaselineTag(releases.Where(r => !r.Draft && r.PublishedAt.HasValue).Select(r => r.TagName), candidate);
        var highestPublished = baselineTag == null ? null : releases.First(r => !r.Draft && r.PublishedAt.HasValue && r.TagName == baselineTag);

        if (highestPublished == null)
        {
            return null;
        }

        var asset = highestPublished.Assets.FirstOrDefault(a => a.Name == ManifestAssetName);
        if (asset == null)
        {
            return null;
        }

        try
        {
            using var http = _httpClientFactory();
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.Url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            request.Headers.Authorization = new AuthenticationHeaderValue("token", _token);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("NukeBuild", "1.0"));

            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new SurfaceException(SurfaceErrorCode.BaselineUnreadable, $"Could not download '{ManifestAssetName}' from release '{highestPublished.TagName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Picks the tag of the highest published stable release below <paramref name="candidate"/>,
    /// ignoring pre-releases and tags that are not versions. Returns null when there is none.
    /// </summary>
    internal static string? SelectBaselineTag(IEnumerable<string?> publishedTags, SemanticVersion candidate)
    {
        return publishedTags
            .Select(tag => (Tag: tag, Ok: SemanticVersion.TryParse(tag, out var version), Version: version))
            .Where(t => t.Ok && t.Version.PreRelease == null && t.Version.CompareTo(candidate) < 0)
            .OrderByDescending(t => t.Version)
            .Select(t => t.Tag)
            .FirstOrDefault();
    }
}

/// <summary>
/// A MAJOR.MINOR.PATCH[-PRERELEASE] version, optionally "v"-prefixed, ordered by SemVer 2.0.0
/// precedence: a pre-release is below its release, and pre-release identifiers compare numerically
/// when both are numeric and ordinally otherwise.
/// </summary>
internal readonly record struct SemanticVersion(int Major, int Minor, int Patch, string? PreRelease) : IComparable<SemanticVersion>
{
    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        text = text[0] is 'v' or 'V' ? text[1..] : text;

        var plus = text.IndexOf('+');
        text = plus >= 0 ? text[..plus] : text;

        var dash = text.IndexOf('-');
        var core = dash >= 0 ? text[..dash] : text;
        var preRelease = dash >= 0 ? text[(dash + 1)..] : null;

        var parts = core.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var patch)
            || preRelease is { Length: 0 })
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, preRelease);

        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var core = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (core != 0)
        {
            return core;
        }

        if (PreRelease == null || other.PreRelease == null)
        {
            // A release ranks above any of its pre-releases.
            return (PreRelease == null).CompareTo(other.PreRelease == null);
        }

        var mine = PreRelease.Split('.');
        var theirs = other.PreRelease.Split('.');

        for (var i = 0; i < Math.Min(mine.Length, theirs.Length); i++)
        {
            var mineIsNumber = long.TryParse(mine[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var mineNumber);
            var theirsIsNumber = long.TryParse(theirs[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var theirsNumber);

            var result = (mineIsNumber, theirsIsNumber) switch
            {
                (true, true) => mineNumber.CompareTo(theirsNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(mine[i], theirs[i]),
            };

            if (result != 0)
            {
                return result;
            }
        }

        return mine.Length.CompareTo(theirs.Length);
    }
}
