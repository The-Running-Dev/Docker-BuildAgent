namespace Parameters;

/// <summary>
/// Represents the parameters used for configuring a Node.js template build process, including repository details,
/// directory paths, and build options.
/// </summary>
public class NodeTemplateParams : ForgeParams
{
    /// <summary>
    /// Gets or sets the directory name for the documentation app (relative to WorkingDir).
    /// This is where the template files will be copied and the app will be built.
    /// </summary>
    public string AppDir { get; set; } = "documentation";

    /// <summary>
    /// Gets or sets the Git repository URL containing the documentation template.
    /// Should be a public repository accessible via HTTPS.
    /// </summary>
    public string NodeTemplateRepositoryUrl { get; set; } = "https://github.com/The-Running-Dev/Docusaurus-Template.git";

    /// <summary>
    /// Gets or sets the local directory path where the template repository will be cloned.
    /// This is a temporary directory that gets created during the build process.
    /// </summary>
    public string NodeTemplateDirPath { get; set; } = "/node-template";

    /// <summary>
    /// Gets or sets a value indicating whether to skip running the package manager install command.
    /// Useful for CI/CD scenarios where dependencies are installed separately.
    /// </summary>
    public bool SkipInstall { get; set; } = false;

    /// <summary>
    /// Gets or sets the package manager to use (npm, pnpm, yarn).
    /// If not specified, will auto-detect based on lock files in the target directory.
    /// </summary>
    public string PackageManager { get; set; } = "";

    /// <summary>
    /// Gets or sets the directory path where artifacts are stored.
    /// Used for integration with other build tools.
    /// </summary>
    public string ArtifactsDir { get; set; } = "/nuke/forge";

    /// <summary>
    /// Gets or sets a value indicating whether to build for production deployment.
    /// When true (default), runs 'build:prod' script after installing dependencies.
    /// </summary>
    public bool IsProduction { get; set; } = true;
}
