using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Serilog;
using Octokit;

using Parameters;

namespace Utilities;

/// <summary>
/// Provides methods for interacting with GitHub repositories, including creating releases.
/// </summary>
public static class GitHub
{
    /// <summary>
    /// Creates or updates a GitHub release for a specified repository using the provided Docker parameters.
    /// </summary>
    /// <remarks>This method constructs the release notes by combining Docker image asset links with the
    /// changelog. It then attempts to find an existing release by the specified tag. If a release is found, it updates
    /// the release; otherwise, it creates a new release. The method requires a valid GitHub repository URL and
    /// appropriate credentials.</remarks>
    /// <param name="p">The <see cref="DockerParams"/> object containing the necessary parameters for the release, including tags,
    /// repository URL, and release tag.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="p"/>.RepositoryUrl is not a valid GitHub URL.</exception>
    public static async Task CreateRelease(DockerParams p)
    {
        var releaseNotes = Git.ChangeLog ?? string.Empty;

        // Compose Docker image asset links
        var assetsText = "## Images\n";
        foreach (var tag in p.Tags)
        {
            assetsText += $"- {tag}\n\n";
            assetsText += $"```\ndocker pull {tag}\n```\n\n";
        }
        assetsText += $"## CHANGELOG\n";

        var body = assetsText + releaseNotes;

        // Parse owner and repo from RepositoryUrl
        var match = Regex.Match(p.RepositoryUrl, @"github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+)");
        if (!match.Success)
        {
            throw new ArgumentException("RepositoryUrl must be a valid GitHub URL.");
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value.Replace(".git", "");

        var client = new GitHubClient(new Octokit.ProductHeaderValue("NukeBuild"))
        {
            Credentials = new Credentials(p.RegistryToken)
        };

        // Try to get the release by tag
        Release release = null;
        try
        {
            release = await client.Repository.Release.Get(owner, repo, p.ReleaseTag);
        }
        catch (NotFoundException)
        {
            // Release does not exist
        }

        if (release != null)
        {
            // Update existing release
            var update = new ReleaseUpdate
            {
                TagName = p.ReleaseTag,
                Name = p.ReleaseTag,
                Body = body,
                Draft = false,
                Prerelease = false
            };

            await client.Repository.Release.Edit(owner, repo, release.Id, update);
            
            Log.Information($"🚀 GitHub Release {p.ReleaseTag} Updated...");
        }
        else
        {
            // Create new release
            var newRelease = new NewRelease(p.ReleaseTag)
            {
                Name = p.ReleaseTag,
                Body = body,
                Draft = false,
                Prerelease = false
            };

            await client.Repository.Release.Create(owner, repo, newRelease);
            
            Log.Information($"🚀 GitHub Release {p.ReleaseTag} Created...");
        }
    }
}