using System;

using Nuke.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Services;
using Parameters;
using Extensions;

namespace Components;

/// <summary>
/// Build component for Docker operations including building and pushing images
/// </summary>
public interface IDockerComponent : INukeBuild
{
    /// <summary>
    /// Gets the service provider for dependency injection
    /// </summary>
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Gets the Docker parameters
    /// </summary>
    DockerParams Parameters { get; }

    /// <summary>
    /// Gets whether this is a local build
    /// </summary>
    bool IsLocalBuild { get; }

    /// <summary>
    /// Gets whether this is a dry run
    /// </summary>
    bool DryRun { get; }

    /// <summary>
    /// Gets whether force push is enabled
    /// </summary>
    bool ForcePush { get; }

    /// <summary>
    /// Gets the registry token
    /// </summary>
    string? RegistryToken { get; }

    /// <summary>
    /// Docker service instance
    /// </summary>
    IDockerService DockerService => ServiceProvider.GetRequiredService<IDockerService>();

    /// <summary>
    /// Logger instance
    /// </summary>
    ILogger<NukeBuild> Logger => ServiceProvider.GetRequiredService<ILogger<NukeBuild>>();

    /// <summary>
    /// Target for building Docker image
    /// </summary>
    Target BuildDockerImage => _ => _
        .Executes(() =>
        {
            DockerService.Build(Parameters);
        });

    /// <summary>
    /// Target for pushing Docker images to registry
    /// </summary>
    Target PushToRegistry => _ => _
        .DependsOn(BuildDockerImage)
        .OnlyWhenDynamic(() => ForcePush || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(RegistryToken))
            {
                Assert.Fail("[ERROR] RegistryToken is Not Set.");
            }

            DockerService.Push(Parameters);
            Logger.Push($"Docker Images: {Parameters.Version.Version}, latest");
        });
}