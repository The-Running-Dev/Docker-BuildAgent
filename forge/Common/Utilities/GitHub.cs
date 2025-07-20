using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Nuke.Common.ChangeLog;

using Parameters;

using Serilog;

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

        var repo = Regex.Replace(p.RepositoryUrl, @"(https?:\/\/)?(www\.)?(ghcr\.io|github\.com)\/", "");
        var apiBase = $"https://api.github.com/repos/{repo}";
        var tagName = p.Version.Version;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NukeBuild", "1.0"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", p.RegistryToken);

        // 1. Try to get the release by tag
        var getReleaseUrl = $"{apiBase}/releases/tags/{tagName}";
        HttpResponseMessage getResponse = null;
        try
        {
            getResponse = await client.GetAsync(getReleaseUrl);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error while checking for existing GitHub release.");
        }

        if (getResponse is { IsSuccessStatusCode: true })
        {
            // Release exists, update it
            var release = await getResponse.Content.ReadFromJsonAsync<dynamic>();
            var releaseId = release.id;

            var updatePayload = new
            {
                tag_name = tagName,
                name = tagName,
                body = body,
                draft = false,
                prerelease = false
            };

            var updateUrl = $"{apiBase}/releases/{releaseId}";
            var updateResponse = await client.PatchAsJsonAsync(updateUrl, updatePayload);

            if (updateResponse.IsSuccessStatusCode)
            {
                Log.Information($"🚀 GitHub Release {tagName} Updated...");
            }
            else
            {
                var error = await updateResponse.Content.ReadAsStringAsync();

                Log.Error($"❌ Failed to Update GitHub Release: {updateResponse.StatusCode}\n{error}");
            }
        }
        else
        {
            // Release does not exist, create it
            var createPayload = new
            {
                tag_name = tagName,
                name = tagName,
                body = body,
                draft = false,
                prerelease = false
            };

            var createUrl = $"{apiBase}/releases";
            var createResponse = await client.PostAsJsonAsync(createUrl, createPayload);

            if (createResponse.IsSuccessStatusCode)
            {
                Log.Information($"🚀 GitHub Release {tagName} Created...");
            }
            else
            {
                var error = await createResponse.Content.ReadAsStringAsync();

                Log.Error($"❌ Failed to Create GitHub Release: {createResponse.StatusCode}\n{error}");
            }
        }
    }
}

// Helper extension for PATCH
public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string requestUri, T value)
    {
        var content = JsonContent.Create(value);
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), requestUri) { Content = content };
        return await client.SendAsync(request);
    }
}