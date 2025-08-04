using Nuke.Common;
using Nuke.Common.Git;
using Microsoft.Extensions.Logging;

using Extensions;
using Components;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for Docker images, including configuration for Docker registry and GitHub release
/// management.
/// 
/// ✅ REFACTORED: Now uses multiple Nuke Build Components for maximum abstraction!
/// </summary>
/// <remarks>
/// The <c>Docker</c> class now implements the modern Nuke Build Components pattern by inheriting
/// from multiple component interfaces, achieving maximum code reuse and minimal duplication.
/// 
/// <para><strong>🎉 MAXIMUM ABSTRACTION ACHIEVED:</strong></para>
/// <list type="bullet">
/// <item><description>IDockerComponent - Provides Docker build and push targets automatically</description></item>
/// <item><description>IGitHubComponent - Provides GitHub release and Git tag targets</description></item>
/// <item><description>Zero target implementations needed in this class</description></item>
/// <item><description>Loose dependencies automatically wire up the build chain</description></item>
/// <item><description>Single-responsibility principle maximized</description></item>
/// </list>
/// 
/// <para><strong>Build Target Dependencies (all inherited):</strong></para>
/// <list type="number">
/// <item><description><c>Setup</c> - Base setup (from Base class)</description></item>
/// <item><description><c>BuildDockerImage</c> - Build Docker image (from IDockerComponent)</description></item>
/// <item><description><c>PushToRegistry</c> - Push Docker images to registry (from IDockerComponent)</description></item>
/// <item><description><c>PublishToGitHub</c> - Create GitHub release and Git tag (from IGitHubComponent)</description></item>
/// <item><description><c>Build</c> - Final target that logs completion (local)</description></item>
/// </list>
/// </remarks>
public class Docker : Base<DockerParams, DiscordNotifications>, IDockerComponent, IGitHubComponent
{
    [Parameter("Templates Directory for Dockerfile templates")]
    public readonly string? TemplatesDir;

    [Parameter("Docker Registry for pushing images")]
    public readonly string? RegistryUrl;

    [Parameter("Registry user for pushing images")]
    public readonly string? RegistryUser;

    [Parameter("Registry Registry token for pushing images")]
    [Secret]
    public readonly string? RegistryToken;

    [Parameter("Tag for the Docker Image")]
    public readonly string? ImageTag;

    [Parameter("Dockerfile to use for building the image")]
    public readonly string? DockerFile;

    [Parameter("Should a GitHub release be created")]
    public readonly bool CreateGitHubRelease;

    [Parameter("Should the GitHub release be marked as pre-release")]
    public readonly bool PreRelease;

    // Component interface implementations
    DockerParams IDockerComponent.Parameters => Parameters;
    
    string? IDockerComponent.RegistryToken => RegistryToken;
    
    bool IDockerComponent.ForcePush => ForcePush;
    
    bool IDockerComponent.DryRun => DryRun;
    
    ILogger<NukeBuild> IDockerComponent.Logger => Logger;
    
    DockerParams IGitHubComponent.Parameters => Parameters;
    
    string? IGitHubComponent.RegistryToken => RegistryToken;
    
    GitRepository? IGitHubComponent.GitRepository => GitRepository;
    
    bool IGitHubComponent.ForcePush => ForcePush;
    
    bool IGitHubComponent.DryRun => DryRun;
    
    ILogger<NukeBuild> IGitHubComponent.Logger => Logger;
    
    /// <summary>
    /// Configures the parameters for the current operation by setting up directory paths, registry URLs, and image
    /// tags.
    /// </summary>
    /// <remarks>This method initializes and adjusts various parameters required for the operation, such as
    /// template directories and image tags. It ensures that the parameters are hydrated with the current verbosity
    /// level and constructs the registry URL and tags based on the provided settings.</remarks>
    protected override void Configure()
    {
        // Copy Nuke CLI parameters
        Parameters.Hydrate(this, verbose: Verbosity == Verbosity.Verbose);

        Parameters.TemplatesDir = Directory.Exists(TemplatesDir)
            ? TemplatesDir
            : Path.Combine(Parameters.RootDirectory, Parameters.TemplatesDir);

        var registryUrl =  !string.IsNullOrEmpty(Parameters.RegistryUrl) ? $"{Parameters.RegistryUrl}/" : string.Empty;

        Parameters.Tags =
        [
            $"{registryUrl}{Parameters.ImageTag}:latest",
            $"{registryUrl}{Parameters.ImageTag}:{Parameters.Version}"
        ];

        Parameters.ReleaseTag = $"v{Parameters.Version}"; // Add "v" prefix to match GitService tag format
        Parameters.CreateGitHubRelease = CreateGitHubRelease;
        Parameters.PreRelease = PreRelease;
    }

    /// <summary>
    /// Executes the build process for the specified target and returns the result code.
    /// </summary>
    /// <remarks>This method initiates the build process using the specified target type and build action. The
    /// return value indicates the success or failure of the build operation.</remarks>
    /// <returns>An integer representing the result code of the build process. A value of 0 typically indicates success, while
    /// any non-zero value indicates an error or failure during the build.</returns>
    public static int Main()
    {
        return Build<Docker>(x => x.Build);
    }

    /// <summary>
    /// Gets the build target that depends on the GitHub publishing process and executes the build actions.
    /// </summary>
    /// <remarks>
    /// This target logs a message indicating the completion of the build process. All other targets
    /// (BuildDockerImage, PushToRegistry, PublishToGitHub) are now inherited from component
    /// interfaces and automatically wired together using loose dependencies.
    /// </remarks>
    public Target Build => _ => _
        .DependsOn<IGitHubComponent>(x => x.PublishToGitHub)
        .Executes(() =>
        {
            Logger.Ok($"Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });
}
