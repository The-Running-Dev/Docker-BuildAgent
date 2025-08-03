using System.IO;

using Nuke.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Services;
using Utilities;
using Extensions;
using Parameters;
using Notifications;
using Components;

/// <summary>
/// Represents a combined build process that first builds a Node.js application and then packages it as a Docker image.
/// 
/// ✅ REFACTORED: Ready for BuildTargets components integration!
/// </summary>
/// <remarks>
/// This class extends the <see cref="Base{TParams, TNotifications}"/> class, providing specific
/// parameters and targets for both Node.js application building and Docker image management.
///
/// <para><strong>Build Target Dependencies (in execution order):</strong></para>
/// <list type="number">
/// <item><description><c>Setup</c> - Base setup (from Base class)</description></item>
/// <item><description><c>Clean</c> - Clean artifacts directory</description></item>
/// <item><description><c>GenerateEnvironment</c> - Generate environment files</description></item>
/// <item><description><c>BuildApplication</c> - Build Node.js application</description></item>
/// <item><description><c>CopyToArtifacts</c> - Copy Node.js build output to artifacts</description></item>
/// <item><description><c>BuildDockerImage</c> - Build Docker image from artifacts</description></item>
/// <item><description><c>PushToRegistry</c> - Push Docker images to registry</description></item>
/// <item><description><c>PublishToGitHub</c> - Create GitHub release and Git tag</description></item>
/// <item><description><c>Build</c> - Final target that logs completion</description></item>
/// </list>
/// </remarks>
public class NodeInDocker : Base<NodeInDockerParams, DiscordNotifications>
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
    
    // Injected services
    private INodeService NodeService => ServiceProvider.GetRequiredService<INodeService>();
    private IDockerService DockerService => ServiceProvider.GetRequiredService<IDockerService>();

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
    /// Gets the build target that depends on the PublishToGitHub target and executes the complete build process.
    /// </summary>
    /// <remarks>This target logs a message indicating the completion of the entire build process, including both
    /// Node.js application building and Docker image creation.</remarks>
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
    /// Git repository URL and registry token are specified.</remarks>
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
    /// <remarks>This target depends on the CopyToArtifacts target and builds the Docker image using the configured parameters.</remarks>
    public Target BuildDockerImage => _ => _
        .DependsOn(CopyToArtifacts)
        .Executes(() =>
        {
            DockerService.Build(Parameters);
        });

    /// <summary>
    /// Gets the target that copies the build output to the artifacts directory.
    /// </summary>
    /// <remarks>This target depends on the successful completion of the <see cref="BuildApplication"/>
    /// target. It executes the process of copying files to the specified artifacts location using the provided
    /// parameters.</remarks>
    public Target CopyToArtifacts => _ => _
        .DependsOn(BuildApplication)
        .Executes(() =>
        {
            NodeService.CopyToArtifacts(Parameters.ToNodeParams());
        });

    /// <summary>
    /// Gets the target that builds the application by executing the necessary build steps.
    /// </summary>
    /// <remarks>This target depends on the successful execution of the <see cref="GenerateEnvironment"/>
    /// target. It performs the build process using the specified parameters.</remarks>
    public Target BuildApplication => _ => _
        .DependsOn(GenerateEnvironment)
        .Executes(() =>
        {
            NodeService.Build(Parameters.ToNodeParams());
        });

    /// <summary>
    /// Gets the target that generates the environment configuration file.
    /// </summary>
    /// <remarks>This target depends on the <c>Clean</c> target and executes the generation of the environment
    /// file. If the environment file is missing values, the process will fail with an assertion error.</remarks>
    public Target GenerateEnvironment => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            if (!Files.GenerateEnvironmentFile(Parameters.Config.AppEnvMapFilePath, Parameters.Config.AppEnvFilePath))
            {
                Assert.Fail($"[ERROR] App Env File Missing Values, Check {Parameters.Config.AppEnvMapFile}");
            }
        });

    /// <summary>
    /// Gets the target that cleans the artifacts directory.
    /// </summary>
    /// <remarks>This target depends on the <see cref="Setup"/> target and ensures that the artifacts
    /// directory is deleted and recreated.</remarks>
    public Target Clean => _ => _
        .DependsOn(Setup)
        .Executes(() =>
        {
            if (Directory.Exists(Parameters.ArtifactsDir))
            {
                Directory.Delete(Parameters.ArtifactsDir, true);
                Logger.Ok("Cleaned Artifacts Directory");
            }

            Directory.CreateDirectory(Parameters.ArtifactsDir);
        });
}
