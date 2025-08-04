using Nuke.Common;
using Microsoft.Extensions.Logging;

using Components;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build node that manages the configuration and execution of build targets.
/// 
/// ✅ REFACTORED: Now uses multiple Nuke Build Components for maximum abstraction!
/// </summary>
/// <remarks>
/// The <c>Node</c> class now implements the modern Nuke Build Components pattern by inheriting
/// from multiple component interfaces, achieving maximum code reuse and minimal duplication.
/// 
/// <para><strong>🎉 MAXIMUM ABSTRACTION ACHIEVED:</strong></para>
/// <list type="bullet">
/// <item><description>ICleanComponent - Provides Clean target automatically</description></item>
/// <item><description>INodeComponent - Provides all Node.js build targets</description></item>
/// <item><description>Zero target implementations needed in this class</description></item>
/// <item><description>Loose dependencies automatically wire up the build chain</description></item>
/// <item><description>Single-responsibility principle maximized</description></item>
/// </list>
/// 
/// <para><strong>Build Target Dependencies (all inherited):</strong></para>
/// <list type="number">
/// <item><description><c>Setup</c> - Base setup (from Base class)</description></item>
/// <item><description><c>Clean</c> - Clean artifacts directory (from ICleanComponent)</description></item>
/// <item><description><c>GenerateEnvironment</c> - Generate environment files (from INodeComponent)</description></item>
/// <item><description><c>BuildApplication</c> - Build Node.js application (from INodeComponent)</description></item>
/// <item><description><c>CopyToArtifacts</c> - Copy build output to artifacts (from INodeComponent)</description></item>
/// <item><description><c>Build</c> - Final target that logs completion (local)</description></item>
/// </list>
/// </remarks>
public class Node : Base<NodeParams, DiscordNotifications>, ICleanComponent, INodeComponent
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

    // Component interface implementations
    string ICleanComponent.ArtifactsDir => Parameters.ArtifactsDir;

    ILogger<NukeBuild> ICleanComponent.Logger => Logger;

    ILogger<NukeBuild> INodeComponent.Logger => Logger;

    NodeParams INodeComponent.Parameters => Parameters;

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
    /// <remarks>
    /// This target logs a message indicating the completion of the build process. All other targets
    /// (Clean, GenerateEnvironment, BuildApplication, CopyToArtifacts) are now inherited from component
    /// interfaces and automatically wired together using loose dependencies.
    /// </remarks>
    public Target Build => _ => _
        .DependsOn<INodeComponent>(x => x.CopyToArtifacts)
        .Executes(() =>
        {
            Logger.Ok($"Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });
}