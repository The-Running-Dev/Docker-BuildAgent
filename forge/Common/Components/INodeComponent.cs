using System;

using Nuke.Common;
using Microsoft.Extensions.DependencyInjection;

using Services;
using Utilities;
using Parameters;

namespace Components;

/// <summary>
/// Build component for Node.js operations
/// </summary>
public interface INodeComponent : INukeBuild
{
    /// <summary>
    /// Gets the service provider
    /// </summary>
    IServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Gets the Node parameters
    /// </summary>
    NodeParams Parameters { get; }

    /// <summary>
    /// Node service instance
    /// </summary>
    INodeService NodeService => ServiceProvider.GetRequiredService<INodeService>();

    /// <summary>
    /// Target for generating environment configuration
    /// </summary>
    Target GenerateEnvironment => _ => _
        .Executes(() =>
        {
            if (!Files.GenerateEnvironmentFile(Parameters.Config.AppEnvMapFilePath, Parameters.Config.AppEnvFilePath))
            {
                Assert.Fail($"[ERROR] App Env File Missing Values, Check {Parameters.Config.AppEnvMapFile}");
            }
        });

    /// <summary>
    /// Target for building the Node.js application
    /// </summary>
    Target BuildApplication => _ => _
        .DependsOn(GenerateEnvironment)
        .Executes(() =>
        {
            NodeService.Build(Parameters);
        });

    /// <summary>
    /// Target for copying build output to artifacts
    /// </summary>
    Target CopyToArtifacts => _ => _
        .DependsOn(BuildApplication)
        .Executes(() =>
        {
            NodeService.CopyToArtifacts(Parameters);
        });
}