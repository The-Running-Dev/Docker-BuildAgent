#nullable enable

using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Octokit;
using Microsoft.Extensions.Logging;

using Entities;
using Utilities;
using Parameters;

namespace Services;

/// <summary>
/// Interface for GitHubService operations to enable testing and dependency injection.
/// </summary>
/// <remarks>
/// This interface abstracts GitHubService API operations, particularly around release management.
/// It enables proper testing by allowing the GitHubService client to be mocked and provides
/// a clean abstraction for GitHubService-related operations.
/// </remarks>
public interface IGitHubService
{
    /// <summary>
    /// Creates or updates a GitHubService release for a specified repository using the provided Docker parameters.
    /// </summary>
    /// <param name="parameters">The DockerParams object containing the necessary parameters for the release.</param>
    /// <param name="options">Optional custom options for the GitHubService release. If null, uses default options.</param>
    /// <param name="formatOptions">Optional custom formatting options for the changelog. If null, uses default formatting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if parameters is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the repository URL is not a valid GitHubService URL.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the release creation fails due to GitHubService API errors.</exception>
    Task CreateRelease(DockerParams parameters, GitHubReleaseOptions? options = null, ChangeLogFormatOptions? formatOptions = null);

    /// <summary>
    /// Validates that a repository URL is a valid GitHubService URL and returns the parsed owner and repository name.
    /// </summary>
    /// <param name="repositoryUrl">The GitHubService repository URL to validate.</param>
    /// <returns>A tuple containing the owner and repository name.</returns>
    /// <exception cref="ArgumentException">Thrown if the URL is not a valid GitHubService URL.</exception>
    (string owner, string repository) ParseRepositoryUrl(string repositoryUrl);

    /// <summary>
    /// Formats release body content from Docker tags, release notes, and custom options.
    /// </summary>
    /// <param name="parameters">The DockerParams object containing the necessary parameters for the release.</param>
    /// <param name="options">Optional custom options for the GitHubService release. If null, uses default options.</param>
    /// <param name="formatOptions">Optional custom formatting options for the changelog. If null, uses default formatting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if parameters is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the repository URL is not a valid GitHubService URL.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the release creation fails due to GitHubService API errors.</exception>
    string FormatReleaseBody(List<string> dockerTags, string releaseNotes, GitHubReleaseOptions? options = null);

    /// <summary>
    /// Checks if a GitHubService release exists for the specified tag.
    /// </summary>
    /// <param name="repositoryUrl">The GitHubService repository URL.</param>
    /// <param name="releaseTag">The release tag to check for.</param>
    /// <param name="token">The GitHubService access token.</param>
    /// <returns>A task that returns true if the release exists, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown if the repository URL is not a valid GitHubService URL.</exception>
    Task<bool> ReleaseExists(string repositoryUrl, string releaseTag, string token);

    /// <summary>
    /// Gets release information for the specified tag.
    /// </summary>
    /// <param name="repositoryUrl">The GitHubService repository URL.</param>
    /// <param name="releaseTag">The release tag to get information for.</param>
    /// <param name="token">The GitHubService access token.</param>
    /// <returns>A task that returns the release information if found, null otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown if the repository URL is not a valid GitHubService URL.</exception>
    Task<GitHubReleaseInfo?> GetRelease(string repositoryUrl, string releaseTag, string token);
}

/// <summary>
/// Default implementation of IGitHubService that performs actual GitHubService API operations.
/// </summary>
/// <remarks>
/// This implementation uses the Octokit library to perform GitHubService API operations.
/// It provides robust error handling, logging, and parameter validation.
/// </remarks>
public class GitHubService : IGitHubService
{
    private readonly ILogger<GitHubService> _logger;

    private readonly IGitService _gitService;
    
    private readonly IChangeLogConfigService _changeLogConfigService;
    
    public GitHubService(IGitService gitService, IChangeLogConfigService changeLogConfigService, ILogger<GitHubService> logger)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _changeLogConfigService = changeLogConfigService ?? throw new ArgumentNullException(nameof(changeLogConfigService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates or updates a GitHubService release for a specified repository using the provided Docker parameters.
    /// </summary>
    /// <param name="parameters">The DockerParams object containing the necessary parameters for the release.</param>
    /// <param name="options">Optional custom options for the GitHubService release. If null, uses default options.</param>
    /// <param name="formatOptions">Optional custom formatting options for the changelog. If null, uses default formatting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CreateRelease(DockerParams parameters, GitHubReleaseOptions? options = null, ChangeLogFormatOptions? formatOptions = null)
    {
        ValidateParameters(parameters);

        // Use provided options or create default ones
        options ??= new GitHubReleaseOptions();

        var changeLogConfig = parameters.ChangeLogConfig ?? _changeLogConfigService.Create(null);

        string releaseNotes = string.Empty;
        if (options.IncludeChangeLog)
        {
            // Use formatOptions parameter first, then options.ChangeLogFormatOptions, then default
            var effectiveFormatOptions = formatOptions ?? options.ChangeLogFormatOptions;
            
            releaseNotes = effectiveFormatOptions != null
                ? _gitService.GenerateChangeLog(changeLogConfig, effectiveFormatOptions)
                : _gitService.GenerateChangeLog(changeLogConfig);
        }

        var body = FormatReleaseBody(parameters.Tags, releaseNotes, options);
        var (owner, repo) = ParseRepositoryUrl(parameters.RepositoryUrl);

        var client = CreateGitHubClient(parameters.RegistryToken);

        await CreateOrUpdateReleaseInternal(client, owner, repo, parameters.ReleaseTag, body, options);

        _logger.LogInformation("[GITHUB] Release {ReleaseTag} processed for {Owner}/{Repo}",
            parameters.ReleaseTag, owner, repo);
    }

    /// <summary>
    /// Validates that a repository URL is a valid GitHubService URL and returns the parsed owner and repository name.
    /// </summary>
    /// <param name="repositoryUrl">The GitHubService repository URL to validate.</param>
    /// <returns>A tuple containing the owner and repository name.</returns>
    public (string owner, string repository) ParseRepositoryUrl(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            throw new ArgumentException("Repository URL cannot be null or empty.", nameof(repositoryUrl));
        }

        var match = Regex.Match(repositoryUrl, @"github\.com[:/](?<owner>[^/]+)/(?<repo>[^/]+)");
        if (!match.Success)
        {
            throw new ArgumentException($"'{repositoryUrl}' is not a valid GitHubService URL. Expected format: https://github.com/owner/repo or git@github.com:owner/repo.git", nameof(repositoryUrl));
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value.Replace(".git", "");

        return (owner, repo);
    }

    /// <summary>
    /// Formats release body content from Docker tags, release notes, and custom options.
    /// </summary>
    /// <param name="dockerTags">The list of Docker image tags to include in the release.</param>
    /// <param name="releaseNotes">The changelog content to include.</param>
    /// <param name="options">Optional release formatting options. If null, uses default options.</param>
    /// <returns>A formatted release body string.</returns>
    public string FormatReleaseBody(List<string> dockerTags, string releaseNotes, GitHubReleaseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(dockerTags, nameof(dockerTags));
        
        options ??= new GitHubReleaseOptions();
        var bodyBuilder = new StringBuilder();

        // Add custom sections first
        foreach (var customSection in options.CustomSections)
        {
            bodyBuilder.AppendLine($"## {customSection.Key}");
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine(customSection.Value);
            bodyBuilder.AppendLine();
        }

        // Add Docker images section
        if (options.IncludeDockerImages && dockerTags.Count > 0)
        {
            bodyBuilder.AppendLine("## Images");
            bodyBuilder.AppendLine();

            foreach (var tag in dockerTags)
            {
                bodyBuilder.AppendLine($"- `{tag}`");
                bodyBuilder.AppendLine();
                bodyBuilder.AppendLine("```bash");
                bodyBuilder.AppendLine($"docker pull {tag}");
                bodyBuilder.AppendLine("```");
                bodyBuilder.AppendLine();
            }
        }

        // Add changelog section
        if (options.IncludeChangeLog && !string.IsNullOrWhiteSpace(releaseNotes))
        {
            bodyBuilder.AppendLine("## CHANGELOG");
            bodyBuilder.AppendLine();
            bodyBuilder.Append(releaseNotes);
        }

        return bodyBuilder.ToString();
    }

    /// <summary>
    /// Checks if a GitHubService release exists for the specified tag.
    /// </summary>
    /// <param name="repositoryUrl">The GitHubService repository URL.</param>
    /// <param name="releaseTag">The release tag to check for.</param>
    /// <param name="token">The GitHubService access token.</param>
    /// <returns>A task that returns true if the release exists, false otherwise.</returns>
    public async Task<bool> ReleaseExists(string repositoryUrl, string releaseTag, string token)
    {
        var release = await GetRelease(repositoryUrl, releaseTag, token);

        return release != null;
    }

    /// <summary>
    /// Gets release information for the specified tag.
    /// </summary>
    /// <param name="repositoryUrl">The GitHubService repository URL.</param>
    /// <param name="releaseTag">The release tag to get information for.</param>
    /// <param name="token">The GitHubService access token.</param>
    /// <returns>A task that returns the release information if found, null otherwise.</returns>
    public async Task<GitHubReleaseInfo?> GetRelease(string repositoryUrl, string releaseTag, string token)
    {
        if (string.IsNullOrWhiteSpace(releaseTag))
        {
            throw new ArgumentException("Release tag cannot be null or empty.", nameof(releaseTag));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("GitHubService token cannot be null or empty.", nameof(token));
        }

        var (owner, repo) = ParseRepositoryUrl(repositoryUrl);
        var client = CreateGitHubClient(token);

        try
        {
            var release = await client.Repository.Release.Get(owner, repo, releaseTag);
            return new GitHubReleaseInfo
            {
                Id = release.Id,
                TagName = release.TagName,
                Name = release.Name ?? string.Empty,
                Body = release.Body ?? string.Empty,
                IsDraft = release.Draft,
                Prerelease = release.Prerelease,
                CreatedAt = release.CreatedAt.DateTime,
                PublishedAt = release.PublishedAt?.DateTime,
                Url = release.HtmlUrl ?? string.Empty
            };
        }
        catch (NotFoundException)
        {
            _logger.LogDebug("[GITHUB] Release {ReleaseTag} not Found for {Owner}/{Repo}", releaseTag, owner, repo);
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GITHUB] Failed to Get Release {ReleaseTag} for {Owner}/{Repo}", releaseTag, owner, repo);
            
            throw new InvalidOperationException($"Failed to Get GitHubService Release '{releaseTag}' for {owner}/{repo}: {ex.Message}", ex);
        }
    }

    private void ValidateParameters(DockerParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(parameters.RepositoryUrl))
        {
            throw new ArgumentException("RepositoryUrl cannot be null or empty.", nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(parameters.ReleaseTag))
        {
            throw new ArgumentException("ReleaseTag cannot be null or empty.", nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(parameters.RegistryToken))
        {
            throw new ArgumentException("RegistryToken cannot be null or empty.", nameof(parameters));
        }
    }

    private GitHubClient CreateGitHubClient(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("GitHubService token cannot be null or empty.", nameof(token));
        }

        return new GitHubClient(new ProductHeaderValue("NukeBuild"))
        {
            Credentials = new Credentials(token)
        };
    }

    private async Task CreateOrUpdateReleaseInternal(GitHubClient client, string owner, string repo, 
        string releaseTag, string body, GitHubReleaseOptions options)
    {
        // Try to get the release by tag
        Release? existingRelease = null;
        try
        {
            existingRelease = await client.Repository.Release.Get(owner, repo, releaseTag);
        }
        catch (NotFoundException)
        {
            // Release does not exist, will create a new one
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GITHUB] Failed to Check Existing Release {ReleaseTag} for {Owner}/{Repo}", 
                releaseTag, owner, repo);
            
            throw new InvalidOperationException($"Failed to Check Existing GitHubService Release '{releaseTag}' for {owner}/{repo}: {ex.Message}", ex);
        }

        var releaseName = options.Name ?? releaseTag;

        try
        {
            if (existingRelease != null)
            {
                // Update existing release
                var update = new ReleaseUpdate
                {
                    TagName = releaseTag,
                    Name = releaseName,
                    Body = body,
                    Draft = options.Draft,
                    Prerelease = options.PreRelease
                };

                await client.Repository.Release.Edit(owner, repo, existingRelease.Id, update);

                _logger.LogInformation("[GITHUB] Release {ReleaseTag} Updated for {Owner}/{Repo}", releaseTag, owner, repo);
            }
            else
            {
                // Create new release
                var newRelease = new NewRelease(releaseTag)
                {
                    Name = releaseName,
                    Body = body,
                    Draft = options.Draft,
                    Prerelease = options.PreRelease
                };

                await client.Repository.Release.Create(owner, repo, newRelease);

                _logger.LogInformation("[GITHUB] Release {ReleaseTag} Created for {Owner}/{Repo}", releaseTag, owner, repo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GITHUB] Failed to Create/Update Release {ReleaseTag} for {Owner}/{Repo}", 
                releaseTag, owner, repo);
            
            throw new InvalidOperationException($"Failed to Create/Update GitHubService Release '{releaseTag}' for {owner}/{repo}: {ex.Message}", ex);
        }
    }
}