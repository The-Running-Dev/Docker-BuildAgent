using System.IO;

using Nuke.Common;
using Microsoft.Extensions.DependencyInjection;

using Services;
using Utilities;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build node that manages the configuration and execution of build targets.
/// </summary>
/// <remarks>
/// The <c>Node</c> class extends the <c>Base</c> class with specific parameters and notifications for
/// managing build processes. It provides various targets for building, cleaning, and managing artifacts, and serves as
/// the entry point for the application.
/// 
/// <para><strong>Build Target Dependencies (in execution order):</strong></para>
/// <list type="number">
/// <item><description><c>Setup</c> - Base setup (from Base class)</description></item>
/// <item><description><c>Clean</c> - Clean artifacts directory</description></item>
/// <item><description><c>GenerateEnvironment</c> - Generate environment files</description></item>
/// <item><description><c>BuildApplication</c> - Build Node.js application</description></item>
/// <item><description><c>CopyToArtifacts</c> - Copy build output to artifacts</description></item>
/// <item><description><c>Build</c> - Final target that logs completion</description></item>
/// </list>
/// </remarks>
public class Node : Base<NodeParams, DiscordNotifications>
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

    // Injected services
    private INodeService NodeService => ServiceProvider.GetRequiredService<INodeService>();

    /// <summary>
    /// Configures the build parameters by hydrating them with values from the Nuke CLI and setting the artifacts
    /// directory.
    /// </summary>
    /// <remarks>This method copies the Nuke CLI parameters into the current context, adjusting verbosity
    /// based on the current setting. It also ensures that the artifacts directory is set to an existing path,
    /// defaulting to a path within the root directory if necessary.</remarks>
    protected override void Configure()
    {
        // Copy Nuke CLI parameters
        Parameters.Hydrate(this, verbose: Verbosity == Verbosity.Verbose);

        Parameters.ArtifactsDir = Directory.Exists(ArtifactsDir)
            ? ArtifactsDir
            : Path.Combine(Parameters.RootDirectory, Parameters.ArtifactsDir);
    }

    /// <summary>
    /// Serves as the entry point for the application.
    /// </summary>
    /// <returns>An integer representing the exit code of the application. A return value of 0 typically indicates success.</returns>
    public static int Main()
    {
        return Build<Node>(x => x.Build);
    }

    /// <summary>
    /// Gets the build target that depends on the CopyToArtifacts target and executes the build process.
    /// </summary>
    /// <remarks>This target logs a message indicating the completion of the build process, including the
    /// forge type and target name.</remarks>
    public Target Build => _ => _
        .DependsOn(CopyToArtifacts)
        .Executes(() =>
        {
            Logger.Ok($"Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
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
            NodeService.CopyToArtifacts(Parameters);
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
            NodeService.Build(Parameters);
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