using System;
using System.IO;
using System.Collections.Generic;

using Nuke.Common;
using Microsoft.Extensions.Logging;

using Services;
using Utilities;
using Extensions;
using Parameters;

namespace Components;

/// <summary>
/// Static build components that provide shared target implementations.
/// This approach works around interface visibility limitations while still achieving code deduplication.
/// </summary>
/// <remarks>
/// <para><strong>Alternative to Interface Components:</strong></para>
/// <para>
/// Since the ServiceProvider property in Base classes is protected, direct interface implementation
/// has visibility constraints. These static methods provide the same deduplication benefits while
/// working within the existing architecture.
/// </para>
/// 
/// <para><strong>Usage Pattern:</strong></para>
/// <code>
/// public class Node : Base&lt;NodeParams, DiscordNotifications&gt;
/// {
///     public Target Clean => BuildTargets.CreateCleanTarget(Parameters.ArtifactsDir);
///     public Target GenerateEnvironment => BuildTargets.CreateGenerateEnvironmentTarget(Parameters);
///     // etc.
/// }
/// </code>
/// 
/// <para><strong>Benefits:</strong></para>
/// <list type="bullet">
/// <item><description>Eliminates code duplication</description></item>
/// <item><description>Works with existing Base class architecture</description></item>
/// <item><description>Maintains all functionality</description></item>
/// <item><description>Easy to test and maintain</description></item>
/// </list>
/// </remarks>
public static class BuildTargets
{
    /// <summary>
    /// Creates a standard Clean target implementation with dependency on Setup
    /// </summary>
    /// <param name="artifactsDir">The artifacts directory to clean</param>
    /// <param name="logger">Optional logger for better logging. If null, uses Console.WriteLine</param>
    /// <returns>Configured Clean target</returns>
    public static Target CreateCleanTarget(string artifactsDir, ILogger<NukeBuild>? logger = null)
    {
        return _ => _
            .Executes(() =>
            {
                if (Directory.Exists(artifactsDir))
                {
                    Directory.Delete(artifactsDir, true);

                    if (logger != null)
                    {
                        logger.Ok("Cleaned Artifacts Directory");
                    }
                    else
                    {
                        Console.WriteLine("[OK] Cleaned Artifacts Directory");
                    }
                }

                Directory.CreateDirectory(artifactsDir);
            });
    }

    /// <summary>
    /// Creates a standard GenerateEnvironment target implementation
    /// </summary>
    /// <param name="parameters">Node parameters for environment generation</param>
    /// <returns>Configured GenerateEnvironment target</returns>
    public static Target CreateGenerateEnvironmentTarget(NodeParams parameters)
    {
        return _ => _
            .Executes(() =>
            {
                if (!Files.GenerateEnvironmentFile(parameters.Config.AppEnvMapFilePath, parameters.Config.AppEnvFilePath))
                {
                    Assert.Fail($"[ERROR] App Env File Missing Values, Check {parameters.Config.AppEnvMapFile}");
                }
            });
    }

    /// <summary>
    /// Creates a standard BuildApplication target implementation
    /// </summary>
    /// <param name="nodeService">Node service instance</param>
    /// <param name="parameters">Node parameters</param>
    /// <returns>Configured BuildApplication target</returns>
    public static Target CreateBuildApplicationTarget(INodeService nodeService, NodeParams parameters)
    {
        return _ => _
            .Executes(() =>
            {
                nodeService.Build(parameters);
            });
    }

    /// <summary>
    /// Creates a standard CopyToArtifacts target implementation
    /// </summary>
    /// <param name="nodeService">Node service instance</param>
    /// <param name="parameters">Node parameters</param>
    /// <returns>Configured CopyToArtifacts target</returns>
    public static Target CreateCopyToArtifactsTarget(INodeService nodeService, NodeParams parameters)
    {
        return _ => _
            .Executes(() =>
            {
                nodeService.CopyToArtifacts(parameters);
            });
    }

    /// <summary>
    /// Creates a standard BuildDockerImage target implementation
    /// </summary>
    /// <param name="dockerService">Docker service instance</param>
    /// <param name="parameters">Docker parameters</param>
    /// <returns>Configured BuildDockerImage target</returns>
    public static Target CreateBuildDockerImageTarget(IDockerService dockerService, DockerParams parameters)
    {
        return _ => _
            .Executes(() =>
            {
                dockerService.Build(parameters);
            });
    }

    /// <summary>
    /// Creates a standard PushToRegistry target implementation
    /// </summary>
    /// <param name="dockerService">Docker service instance</param>
    /// <param name="parameters">Docker parameters</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="isLocalBuild">Whether this is a local build</param>
    /// <param name="dryRun">Whether this is a dry run</param>
    /// <param name="forcePush">Whether force push is enabled</param>
    /// <param name="registryToken">Registry token</param>
    /// <returns>Configured PushToRegistry target</returns>
    public static Target CreatePushToRegistryTarget(
        IDockerService dockerService, 
        DockerParams parameters, 
        ILogger<NukeBuild> logger,
        bool isLocalBuild,
        bool dryRun,
        bool forcePush,
        string? registryToken)
    {
        return _ => _
            .OnlyWhenDynamic(() => forcePush || (!isLocalBuild && !dryRun))
            .Executes(() =>
            {
                if (string.IsNullOrWhiteSpace(registryToken))
                {
                    Assert.Fail("[ERROR] RegistryToken is Not Set.");
                }

                dockerService.Push(parameters);
                logger.Push($"Docker Images: {parameters.Version.Version}, latest");
            });
    }

    /// <summary>
    /// Creates a standard PublishToGitHub target implementation
    /// </summary>
    /// <param name="gitHubService">GitHub service instance</param>
    /// <param name="gitService">Git service instance</param>
    /// <param name="parameters">Docker parameters</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="gitRepository">Git repository</param>
    /// <param name="isLocalBuild">Whether this is a local build</param>
    /// <param name="dryRun">Whether this is a dry run</param>
    /// <param name="forcePush">Whether force push is enabled</param>
    /// <param name="registryToken">Registry token</param>
    /// <returns>Configured PublishToGitHub target</returns>
    public static Target CreatePublishToGitHubTarget(
        GitHubService gitHubService,
        GitService gitService,
        DockerParams parameters,
        ILogger<NukeBuild> logger,
        Nuke.Common.Git.GitRepository? gitRepository,
        bool isLocalBuild,
        bool dryRun,
        bool forcePush,
        string? registryToken)
    {
        return _ => _
            .OnlyWhenDynamic(() =>
                parameters.CreateGitHubRelease &&
                (forcePush || (!isLocalBuild && !dryRun)) &&
                !string.IsNullOrWhiteSpace(gitRepository?.HttpsUrl) &&
                !string.IsNullOrWhiteSpace(registryToken))
            .Executes(async () =>
            {
                try
                {
                    await gitHubService.CreateRelease(parameters);
                    logger.Ok("GitHub Release Created Successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to Create GitHub Release");
                    Assert.Fail($"Failed to Create GitHub Release: {ex.Message}");
                }

                try
                {
                    gitService.CreateTag(parameters.ReleaseTag);
                    logger.Tag($"'{parameters.ReleaseTag}' Created Successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to Create Git Tag");
                    Assert.Fail($"Failed to Create Git Tag: {ex.Message}");
                }
            });
    }
}

/// <summary>
/// Extension methods that make using the build targets even easier.
/// These provide a fluent API for build classes to use the shared target implementations.
/// </summary>
public static class BuildTargetExtensions
{
    /// <summary>
    /// Extension method to create Clean target from any build class
    /// </summary>
    public static Target CreateCleanTarget(this INukeBuild build, string artifactsDir, ILogger<NukeBuild>? logger = null)
    {
        return BuildTargets.CreateCleanTarget(artifactsDir, logger);
    }

    /// <summary>
    /// Extension method to create GenerateEnvironment target from any build class
    /// </summary>
    public static Target CreateGenerateEnvironmentTarget(this INukeBuild build, NodeParams parameters)
    {
        return BuildTargets.CreateGenerateEnvironmentTarget(parameters);
    }

    /// <summary>
    /// Extension method to create BuildApplication target from any build class
    /// </summary>
    public static Target CreateBuildApplicationTarget(this INukeBuild build, INodeService nodeService, NodeParams parameters)
    {
        return BuildTargets.CreateBuildApplicationTarget(nodeService, parameters);
    }

    /// <summary>
    /// Extension method to create CopyToArtifacts target from any build class
    /// </summary>
    public static Target CreateCopyToArtifactsTarget(this INukeBuild build, INodeService nodeService, NodeParams parameters)
    {
        return BuildTargets.CreateCopyToArtifactsTarget(nodeService, parameters);
    }

    /// <summary>
    /// Extension method to create BuildDockerImage target from any build class
    /// </summary>
    public static Target CreateBuildDockerImageTarget(this INukeBuild build, IDockerService dockerService, DockerParams parameters)
    {
        return BuildTargets.CreateBuildDockerImageTarget(dockerService, parameters);
    }

    /// <summary>
    /// Extension method to create PushToRegistry target from any build class
    /// </summary>
    public static Target CreatePushToRegistryTarget(this INukeBuild build, 
        IDockerService dockerService, 
        DockerParams parameters, 
        ILogger<NukeBuild> logger,
        bool isLocalBuild,
        bool dryRun,
        bool forcePush,
        string? registryToken)
    {
        return BuildTargets.CreatePushToRegistryTarget(dockerService, parameters, logger, isLocalBuild, dryRun, forcePush, registryToken);
    }

    /// <summary>
    /// Extension method to create PublishToGitHub target from any build class
    /// </summary>
    public static Target CreatePublishToGitHubTarget(this INukeBuild build,
        GitHubService gitHubService,
        GitService gitService,
        DockerParams parameters,
        ILogger<NukeBuild> logger,
        Nuke.Common.Git.GitRepository? gitRepository,
        bool isLocalBuild,
        bool dryRun,
        bool forcePush,
        string? registryToken)
    {
        return BuildTargets.CreatePublishToGitHubTarget(gitHubService, gitService, parameters, logger, gitRepository, isLocalBuild, dryRun, forcePush, registryToken);
    }
}

/// <summary>
/// Advanced extension methods that provide even more convenient ways to use BuildTargets.
/// These methods reduce the boilerplate even further and provide a fluent API.
/// </summary>
public static class AdvancedBuildTargetExtensions
{
    /// <summary>
    /// Creates a complete Node.js build pipeline using shared components
    /// </summary>
    /// <param name="build">The build instance</param>
    /// <param name="nodeService">Node service instance</param>
    /// <param name="parameters">Node parameters</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>A dictionary of configured targets for the Node.js build pipeline</returns>
    public static Dictionary<string, Target> CreateNodePipeline(
        this INukeBuild build,
        INodeService nodeService, 
        NodeParams parameters,
        ILogger<NukeBuild> logger)
    {
        return new Dictionary<string, Target>
        {
            ["Clean"] = BuildTargets.CreateCleanTarget(parameters.ArtifactsDir, logger),
            ["GenerateEnvironment"] = BuildTargets.CreateGenerateEnvironmentTarget(parameters),
            ["BuildApplication"] = BuildTargets.CreateBuildApplicationTarget(nodeService, parameters),
            ["CopyToArtifacts"] = BuildTargets.CreateCopyToArtifactsTarget(nodeService, parameters)
        };
    }

    /// <summary>
    /// Creates a complete Docker build pipeline using shared components
    /// </summary>
    /// <param name="build">The build instance</param>
    /// <param name="dockerService">Docker service instance</param>
    /// <param name="parameters">Docker parameters</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="gitRepository">Git repository</param>
    /// <param name="gitHubService">GitHub service instance</param>
    /// <param name="gitService">Git service instance</param>
    /// <param name="isLocalBuild">Whether this is a local build</param>
    /// <param name="dryRun">Whether this is a dry run</param>
    /// <param name="forcePush">Whether force push is enabled</param>
    /// <param name="registryToken">Registry token</param>
    /// <returns>A dictionary of configured targets for the Docker build pipeline</returns>
    public static Dictionary<string, Target> CreateDockerPipeline(
        this INukeBuild build,
        IDockerService dockerService,
        DockerParams parameters,
        ILogger<NukeBuild> logger,
        Nuke.Common.Git.GitRepository? gitRepository,
        GitHubService gitHubService,
        GitService gitService,
        bool isLocalBuild,
        bool dryRun,
        bool forcePush,
        string? registryToken)
    {
        return new Dictionary<string, Target>
        {
            ["BuildDockerImage"] = BuildTargets.CreateBuildDockerImageTarget(dockerService, parameters),
            ["PushToRegistry"] = BuildTargets.CreatePushToRegistryTarget(dockerService, parameters, logger, isLocalBuild, dryRun, forcePush, registryToken),
            ["PublishToGitHub"] = BuildTargets.CreatePublishToGitHubTarget(gitHubService, gitService, parameters, logger, gitRepository, isLocalBuild, dryRun, forcePush, registryToken)
        };
    }

    /// <summary>
    /// Creates a complete Node-in-Docker build pipeline using shared components
    /// </summary>
    /// <param name="build">The build instance</param>
    /// <param name="nodeService">Node service instance</param>
    /// <param name="dockerService">Docker service instance</param>
    /// <param name="nodeParams">Node parameters</param>
    /// <param name="dockerParams">Docker parameters</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="gitRepository">Git repository</param>
    /// <param name="gitHubService">GitHub service instance</param>
    /// <param name="gitService">Git service instance</param>
    /// <param name="isLocalBuild">Whether this is a local build</param>
    /// <param name="dryRun">Whether this is a dry run</param>
    /// <param name="forcePush">Whether force push is enabled</param>
    /// <param name="registryToken">Registry token</param>
    /// <returns>A dictionary of configured targets for the complete Node-in-Docker pipeline</returns>
    public static Dictionary<string, Target> CreateNodeInDockerPipeline(
        this INukeBuild build,
        INodeService nodeService,
        IDockerService dockerService,
        NodeParams nodeParams,
        DockerParams dockerParams,
        ILogger<NukeBuild> logger,
        Nuke.Common.Git.GitRepository? gitRepository,
        GitHubService gitHubService,
        GitService gitService,
        bool isLocalBuild,
        bool dryRun,
        bool forcePush,
        string? registryToken)
    {
        return new Dictionary<string, Target>
        {
            // Node.js pipeline
            ["Clean"] = BuildTargets.CreateCleanTarget(nodeParams.ArtifactsDir, logger),
            ["GenerateEnvironment"] = BuildTargets.CreateGenerateEnvironmentTarget(nodeParams),
            ["BuildApplication"] = BuildTargets.CreateBuildApplicationTarget(nodeService, nodeParams),
            ["CopyToArtifacts"] = BuildTargets.CreateCopyToArtifactsTarget(nodeService, nodeParams),
            
            // Docker pipeline  
            ["BuildDockerImage"] = BuildTargets.CreateBuildDockerImageTarget(dockerService, dockerParams),
            ["PushToRegistry"] = BuildTargets.CreatePushToRegistryTarget(dockerService, dockerParams, logger, isLocalBuild, dryRun, forcePush, registryToken),
            ["PublishToGitHub"] = BuildTargets.CreatePublishToGitHubTarget(gitHubService, gitService, dockerParams, logger, gitRepository, isLocalBuild, dryRun, forcePush, registryToken)
        };
    }
}