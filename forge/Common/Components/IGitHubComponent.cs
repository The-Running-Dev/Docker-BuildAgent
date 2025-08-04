#nullable enable

using System;

using Extensions;
using Nuke.Common;
using Nuke.Common.Git;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Services;
using Parameters;

namespace Components;

/// <summary>
/// Build component for GitHub release operations
/// </summary>
public interface IGitHubComponent : INukeBuild
{
    /// <summary>
    /// Gets the service provider
    /// </summary>
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Gets the parameters with GitHub release configuration
    /// </summary>
    DockerParams Parameters { get; }

    /// <summary>
    /// Gets the registry token
    /// </summary>
    string? RegistryToken { get; }

    /// <summary>
    /// Gets the git repository information
    /// </summary>
    GitRepository? GitRepository { get; }

    /// <summary>
    /// Gets the force push setting
    /// </summary>
    bool ForcePush { get; }

    /// <summary>
    /// Gets the dry run setting
    /// </summary>
    bool DryRun { get; }

    /// <summary>
    /// Gets the logger instance
    /// </summary>
    ILogger<NukeBuild> Logger { get; }

    /// <summary>
    /// GitHub service instance
    /// </summary>
    GitHubService GitHubService => ServiceProvider.GetRequiredService<GitHubService>();

    /// <summary>
    /// Git service instance
    /// </summary>
    GitService GitService => ServiceProvider.GetRequiredService<GitService>();

    /// <summary>
    /// Target for publishing to GitHub and creating releases
    /// </summary>
    Target PublishToGitHub => _ => _
        .TryDependsOn<IDockerComponent>(x => x.PushToRegistry)
        .OnlyWhenDynamic(() =>
            Parameters.CreateGitHubRelease &&
            (ForcePush || (!IsLocalBuild && !DryRun)) &&
            !string.IsNullOrWhiteSpace(GitRepository?.HttpsUrl) &&
            !string.IsNullOrWhiteSpace(RegistryToken))
        .Executes(async () =>
        {
            try
            {
                // Create GitHub release first
                await GitHubService.CreateRelease(Parameters);
                
                Logger.Ok("GitHub Release Created Successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to Create GitHub Release");

                Assert.Fail($"Failed to Create GitHub Release: {ex.Message}");
            }

            try
            {
                // Create Git tag after successful release
                GitService.CreateTag(Parameters.ReleaseTag);

                Logger.Ok($"'{Parameters.ReleaseTag}' Created Successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to Create Git Tag");

                Assert.Fail($"Failed to Create Git Tag: {ex.Message}");
            }
        });
}