using Nuke.Common;
using Nuke.Common.Git;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Components;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a combined build process that first builds a Node.js application and then packages it as a Docker image.
/// 
/// ✅ REFACTORED: Now uses multiple Nuke Build Components for maximum abstraction!
/// </summary>
/// <remarks>
/// The <c>NodeInDocker</c> class now implements the modern Nuke Build Components pattern by inheriting
/// from multiple component interfaces, achieving maximum code reuse and minimal duplication.
/// 
/// <para><strong>🎉 MAXIMUM ABSTRACTION ACHIEVED:</strong></para>
/// <list type="bullet">
/// <item><description>ICleanComponent - Provides Clean target automatically</description></item>
/// <item><description>INodeComponent - Provides all Node.js build targets</description></item>
/// <item><description>IDockerComponent - Provides Docker build and push targets</description></item>
/// <item><description>IGitHubComponent - Provides GitHub release and Git tag targets</description></item>
/// <item><description>Zero target implementations needed in this class</description></item>
/// <item><description>Loose dependencies automatically wire up the complete build chain</description></item>
/// </list>
/// 
/// <para><strong>Build Target Dependencies (all inherited):</strong></para>
/// <list type="number">
/// <item><description><c>Setup</c> - Base setup (from Base class)</description></item>
/// <item><description><c>Clean</c> - Clean artifacts directory (from ICleanComponent)</description></item>
/// <item><description><c>GenerateEnvironment</c> - Generate environment files (from INodeComponent)</description></item>
/// <item><description><c>BuildApplication</c> - Build Node.js application (from INodeComponent)</description></item>
/// <item><description><c>CopyToArtifacts</c> - Copy Node.js build output to artifacts (from INodeComponent)</description></item>
/// <item><description><c>BuildDockerImage</c> - Build Docker image from artifacts (from IDockerComponent)</description></item>
/// <item><description><c>PushToRegistry</c> - Push Docker images to registry (from IDockerComponent)</description></item>
/// <item><description><c>PublishToGitHub</c> - Create GitHub release and Git tag (from IGitHubComponent)</description></item>
/// <item><description><c>Build</c> - Final target that logs completion (local)</description></item>
/// </list>
/// </remarks>
public class NodeInDocker : Base<NodeInDockerParams, DiscordNotifications>, ICleanComponent, INodeComponent, IDockerComponent, IGitHubComponent
{
    [Parameter("The Artifacts directory")]
    public readonly string? ArtifactsDir;

    [Parameter("Templates Directory for Dockerfile templates")]
    public readonly string? TemplatesDir;

    [Parameter("Docker Registry for pushing images")]
    public readonly string? RegistryUrl;

    [Parameter("Registry user for pushing images")]
    public readonly string? RegistryUser;

    [Parameter("Registry token for pushing images")]
    [Secret]
    public readonly string? RegistryToken;

    [Parameter("Tag for the Docker Image")]
    public readonly string? ImageTag;

    [Parameter("Dockerfile to use for building the image")]
    public readonly string? DockerFile;

    [Parameter("Should a GitHub release be created")]
    public readonly bool CreateGitHubRelease;
    
    // Component interface implementations
    string ICleanComponent.ArtifactsDir => Parameters.ArtifactsDir;
    
    ILogger<NukeBuild> ICleanComponent.Logger => ServiceProvider.GetRequiredService<ILogger<NukeBuild>>();
    
    NodeParams INodeComponent.Parameters => Parameters.ToNodeParams();
    
    ILogger<NukeBuild> INodeComponent.Logger => ServiceProvider.GetRequiredService<ILogger<NukeBuild>>();
    
    DockerParams IDockerComponent.Parameters => Parameters;
    
    string? IDockerComponent.RegistryToken => RegistryToken;
    
    bool IDockerComponent.ForcePush => ForcePush;
    
    bool IDockerComponent.DryRun => DryRun;
    
    ILogger<NukeBuild> IDockerComponent.Logger => ServiceProvider.GetRequiredService<ILogger<NukeBuild>>();
    
    DockerParams IGitHubComponent.Parameters => Parameters;
    
    string? IGitHubComponent.RegistryToken => RegistryToken;
    
    GitRepository? IGitHubComponent.GitRepository => GitRepository;
    
    bool IGitHubComponent.ForcePush => ForcePush;
    
    bool IGitHubComponent.DryRun => DryRun;
    
    ILogger<NukeBuild> IGitHubComponent.Logger => ServiceProvider.GetRequiredService<ILogger<NukeBuild>>();

    // All services are now provided automatically by component interfaces

    /// <summary>
    /// Configures the parameters for the current operation by setting up directory paths, registry URLs, and image tags.
    /// </summary>
    /// <remarks>This method initializes and adjusts various parameters required for both Node and Docker operations,
    /// such as artifacts and template directories, and constructs the registry URL and tags based on the provided settings.</remarks>
    protected override void Configure()
    {
        // Copy Nuke CLI parameters
        Parameters.Hydrate(this, verbose: Verbosity == Verbosity.Verbose);

        // Configure Node-related parameters
        Parameters.ArtifactsDir = Directory.Exists(ArtifactsDir)
            ? ArtifactsDir
            : Path.Combine(Parameters.RootDirectory, Parameters.ArtifactsDir);

        // Configure Docker-related parameters
        Parameters.TemplatesDir = Directory.Exists(TemplatesDir)
            ? TemplatesDir
            : Path.Combine(Parameters.RootDirectory, Parameters.TemplatesDir);

        var registryUrl = !string.IsNullOrEmpty(Parameters.RegistryUrl) ? $"{Parameters.RegistryUrl}/" : string.Empty;

        Parameters.Tags =
        [
            $"{registryUrl}{Parameters.ImageTag}:latest",
            $"{registryUrl}{Parameters.ImageTag}:{Parameters.Version}"
        ];

        Parameters.ReleaseTag = $"v{Parameters.Version}"; // Add "v" prefix to match Git tag format
    }

    /// <summary>
    /// Serves as the entry point for the application.
    /// </summary>
    /// <returns>An integer representing the exit code of the application. A return value of 0 typically indicates success.</returns>
    public static int Main()
    {
        return Build<NodeInDocker>(x => x.Build);
    }

    /// <summary>
    /// Gets the build target that depends on the GitHub publishing process and executes the complete build process.
    /// </summary>
    /// <remarks>
    /// This target logs a message indicating the completion of the entire build process. All other targets
    /// (Clean, GenerateEnvironment, BuildApplication, CopyToArtifacts, BuildDockerImage, PushToRegistry, PublishToGitHub)
    /// are now inherited from component interfaces and automatically wired together using loose dependencies.
    /// </remarks>
    public Target Build => _ => _
        .DependsOn<IGitHubComponent>(x => x.PublishToGitHub)
        .Executes(() =>
        {
            Logger.Ok($"Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");        
        });
}
