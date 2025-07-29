using Nuke.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Services;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for Docker images, including configuration for Docker registry and GitHub release
/// management.
/// </summary>
/// <remarks>
/// This class extends the <see cref="Base{TParams, TNotifications}"/> class, providing specific
/// parameters and targets for building and managing Docker images. It includes functionality for configuring Docker
/// registry details, building Docker images, tagging, pushing to a registry, and optionally creating a GitHub
/// release with associated Git tag.
/// 
/// <para><strong>Build Target Dependencies (in execution order):</strong></para>
/// <list type="number">
/// <item><description><c>Setup</c> - Base setup (from Base class)</description></item>
/// <item><description><c>BuildDockerImage</c> - Build Docker image</description></item>
/// <item><description><c>PushToRegistry</c> - Push Docker images to registry (conditional)</description></item>
/// <item><description><c>PublishToGitHub</c> - Create GitHub release and Git tag (conditional)</description></item>
/// <item><description><c>Build</c> - Final target that logs completion</description></item>
/// </list>
/// </remarks>
public class Docker : Base<DockerParams, DiscordNotifications>
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

    // Injected services via properties from Base class
    private IDockerService DockerService => ServiceProvider.GetRequiredService<IDockerService>();
    
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
    /// Gets the build target that depends on the release creation process and executes the build actions.
    /// </summary>
    /// <remarks>This target logs a message indicating the completion of the build process. It is configured
    /// to depend on the <c>PublishToGitHub</c> target, ensuring that the release is created before the build actions are
    /// executed.</remarks>
    public Target Build => _ => _
        .DependsOn(PublishToGitHub)
        .Executes(() =>
        {
            Logger.Ok($"Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });

    /// <summary>
    /// Gets the target that publishes a release to GitHub and creates the associated Git tag.
    /// </summary>
    /// <remarks>This target depends on the <see cref="PushToRegistry"/> target and executes only under
    /// specific conditions: when creating a GitHub release, not during a local build, not in dry run mode, and when the
    /// Git repository URL and registry token are specified. It also requires either a forced push or a non-local,
    /// non-dry run build. This target creates both the GitHub release (with changelog) and the Git tag in sequence.</remarks>
    public Target PublishToGitHub => _ => _
        .DependsOn(PushToRegistry)
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

                Logger.Tag($"'{Parameters.ReleaseTag}' Created Successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to Create Git Tag");

                Assert.Fail($"Failed to Create Git Tag: {ex.Message}");
            }
        });

    /// <summary>
    /// Gets the target that pushes Docker images to the specified registry.
    /// </summary>
    /// <remarks>This target depends on the <see cref="BuildDockerImage"/> target and executes only when certain conditions are met, 
    /// such as when a force push is requested or during a non-local build without a dry run.</remarks>
    public Target PushToRegistry => _ => _
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

    /// <summary>
    /// Gets the target responsible for building the Docker image.
    /// </summary>
    /// <remarks>This target depends on the Setup target and builds the Docker image using the configured parameters.</remarks>
    public Target BuildDockerImage => _ => _
        .DependsOn(Setup)
        .Executes(() =>
        {
            DockerService.Build(Parameters);
        });
}