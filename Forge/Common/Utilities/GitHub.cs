using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

using Serilog;

using Parameters;

namespace Utilities;

/// <summary>
/// Provides methods for interacting with GitHub repositories, including creating releases.
/// </summary>
public static class GitHub
{
    /// <summary>
    /// Creates a new release on GitHub for the specified Docker image version.
    /// </summary>
    /// <remarks>This method reads the changelog from a local file named "CHANGELOG.md" if it exists, and
    /// includes it in the release notes. It also lists the Docker image tags as part of the release assets. The release
    /// is created as a non-draft and non-prerelease.</remarks>
    /// <param name="p">The parameters required for creating the release, including repository URL, version, tags, and authentication
    /// token.</param>
    /// <returns></returns>
    public static async Task CreateRelease(DockerParams p)
    {
        var changelogPath = "CHANGELOG.md";
        var releaseNotes = File.Exists(changelogPath) ? await File.ReadAllTextAsync(changelogPath) : "No Changelog Available.";

        // Compose Docker image asset links
        var assetsText = "## Images\n";
        foreach (var tag in p.Tags)
        {
            assetsText += $"- {tag}{Environment.NewLine}";
        }
        assetsText += $"{Environment.NewLine}";
        assetsText += $"## CHANGELOG{Environment.NewLine}";

        var body = assetsText + releaseNotes;

        var repo = Regex.Replace(p.RepositoryUrl, @"(https?:\/\/)?(www\.)?(ghcr\.io|github\.com)\/", "");
        var apiUrl = $"https://api.github.com/repos/{repo}/releases";
        var tagName = p.Version.Version;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NukeBuild", "1.0"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", p.Token);

        var payload = new
        {
            tag_name = tagName,
            name = tagName,
            body = body,
            draft = false,
            prerelease = false
        };
        
        var response = await client.PostAsJsonAsync(apiUrl, payload);

        if (response.IsSuccessStatusCode)
        {
            Log.Information($"🚀 GitHub Release {tagName} Created...");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();

            Log.Error($"❌ Failed to Create GitHub Release: {response.StatusCode}\n{error}");
        }
    }
}