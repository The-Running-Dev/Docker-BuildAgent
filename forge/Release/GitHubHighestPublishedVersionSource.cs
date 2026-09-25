#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;

using Octokit;

namespace Release;

/// <summary>
/// The highest published major (I6), read from GitHub releases the same way
/// <c>Surface.GitHubReleaseManifestSource</c> finds the baseline release — published (non-draft),
/// highest by <see cref="ReleaseVersion"/> ordering rather than by publish date, since a
/// back-published older version must not raise the bar.
/// </summary>
public sealed class GitHubHighestPublishedVersionSource : IHighestPublishedVersionSource
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly IGitHubClient _client;

    public GitHubHighestPublishedVersionSource(string owner, string repo, string token, IGitHubClient? client = null)
    {
        _owner = owner;
        _repo = repo;
        _client = client ?? new GitHubClient(new ProductHeaderValue("NukeBuild")) { Credentials = new Credentials(token) };
    }

    public async Task<ReleaseVersion?> GetHighestPublishedAsync()
    {
        var releases = await _client.Repository.Release.GetAll(_owner, _repo).ConfigureAwait(false);

        return releases
            .Where(r => !r.Draft && r.PublishedAt.HasValue)
            .Select(r => TryParse(r.TagName))
            .Where(v => v != null)
            .OrderByDescending(v => v!.Major)
            .ThenByDescending(v => v!.Minor)
            .ThenByDescending(v => v!.Patch)
            .FirstOrDefault();
    }

    private static ReleaseVersion? TryParse(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        try
        {
            return ReleaseVersion.Parse(tagName);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
