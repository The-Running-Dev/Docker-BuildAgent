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
/// Reads surface-manifest.json from the highest published GitHub release, via Octokit for
/// release/asset listing and a plain HttpClient for the asset bytes (Octokit does not expose
/// release asset content directly).
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

    public async Task<string?> GetLatestManifestJsonAsync()
    {
        IReadOnlyList<Release> releases;
        try
        {
            releases = await _client.Repository.Release.GetAll(_owner, _repo);
        }
        catch (Exception ex)
        {
            throw new SurfaceException(SurfaceErrorCode.BaselineUnreadable, $"Could not list releases for {_owner}/{_repo}: {ex.Message}", ex);
        }

        var highestPublished = releases
            .Where(r => !r.Draft && r.PublishedAt.HasValue)
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefault();

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
}
