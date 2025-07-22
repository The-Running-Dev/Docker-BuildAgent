using System.IO;

using Serilog;
using Nuke.Common;

using Utilities;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for Node.js documentation templates, providing targets for cloning template repositories,
/// copying files, and building documentation apps.
/// </summary>
/// <remarks>This class extends <see cref="Base{TParams,TNotifications}"/> to define specific build
/// targets and configurations for Node.js documentation template projects. It includes targets for cloning template
/// repositories, copying template files while preserving customizations, and building production-ready documentation.</remarks>
public class NodeTemplate : Base<NodeTemplateParams, DiscordNotifications>
{
    [Parameter("Directory name for the documentation app")]
    public readonly string AppDir;

    [Parameter("Git repository URL for the template")]
    public readonly string NodeTemplateRepositoryUrl;

    [Parameter("Local path where template will be cloned")]
    public readonly string NodeTemplateDirPath;

    [Parameter("Skip package manager install step")]
    public readonly bool SkipInstall;

    [Parameter("Package manager to use (npm, pnpm, yarn)")]
    public readonly string PackageManager;

    [Parameter("Directory where build artifacts are stored")]
    public readonly string ArtifactsDir;

    [Parameter("Build for production deployment")]
    public readonly bool IsProduction;

    /// <summary>
    /// Configures the build parameters by hydrating them with values from the Nuke CLI and setting default values.
    /// </summary>
    /// <remarks>This method copies the Nuke CLI parameters into the current context and ensures that all
    /// configuration properties are properly set with appropriate defaults if not specified.</remarks>
    protected override void Configure()
    {
        // Copy Nuke CLI parameters
        Parameters.Hydrate(this, verbose: Verbosity == Verbosity.Verbose);

        // Set defaults for parameters not provided
        if (!string.IsNullOrEmpty(AppDir))
            Parameters.AppDir = AppDir;

        if (!string.IsNullOrEmpty(NodeTemplateRepositoryUrl))
            Parameters.NodeTemplateRepositoryUrl = NodeTemplateRepositoryUrl;

        if (!string.IsNullOrEmpty(NodeTemplateDirPath))
            Parameters.NodeTemplateDirPath = NodeTemplateDirPath;

        if (!string.IsNullOrEmpty(PackageManager))
            Parameters.PackageManager = PackageManager;

        if (!string.IsNullOrEmpty(ArtifactsDir))
            Parameters.ArtifactsDir = ArtifactsDir;

        // Handle boolean parameters
        Parameters.SkipInstall = SkipInstall;
        Parameters.IsProduction = IsProduction;

        // Ensure artifacts directory exists
        Parameters.ArtifactsDir = Directory.Exists(Parameters.ArtifactsDir)
            ? Parameters.ArtifactsDir
            : Path.Combine(Parameters.RootDirectory, Parameters.ArtifactsDir);
    }

    /// <summary>
    /// Executes the build process for the specified build type and returns the exit code.
    /// </summary>
    /// <returns>An integer representing the exit code of the build process. A value of 0 typically indicates success, while any
    /// other value indicates an error.</returns>
    // public static int Main()
    // {
    //     return Build<NodeTemplate>(x => x.Build);
    // }

    /// <summary>
    /// Gets the main build target that orchestrates the entire Node.js template build process.
    /// </summary>
    public Target Build => _ => _
        .DependsOn(BuildDocumentation)
        .Executes(() =>
        {
            Log.Information($"✅ Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });

    /// <summary>
    /// Gets the build target that creates the documentation from the template.
    /// </summary>
    public Target BuildDocumentation => _ => _
        .DependsOn(SetupBuild)
        .Executes(() =>
        {
            Utilities.NodeTemplate.Build(Parameters);
        });

    /// <summary>
    /// Gets the setup target that initializes the build environment.
    /// </summary>
    public Target SetupBuild => _ => _
        .Executes(() =>
        {
            Log.Information("🔧 Setting Up Node Template Build Environment...");
            
            // Ensure the artifacts directory exists
            if (!Directory.Exists(Parameters.ArtifactsDir))
            {
                Directory.CreateDirectory(Parameters.ArtifactsDir);

                Log.Information($"📁 Created Artifacts Directory: {Parameters.ArtifactsDir}");
            }

            Log.Information("✅ Setup Complete");
        });
}
