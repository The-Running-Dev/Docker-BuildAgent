using System.IO;

using Serilog;
using Nuke.Common;

using Utilities;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for a Node.js application, providing targets for building, cleaning, and managing
/// artifacts.
/// </summary>
/// <remarks>This class extends <see cref="Base{TParams,TNotifications}"/> to define specific build
/// targets and configurations for a Node.js project. It includes targets for building the application, copying
/// artifacts, and cleaning up directories.</remarks>
public class Node : Base<NodeParams, DiscordNotifications>
{
    [Parameter("The Artifacts directory")]
    public readonly string ArtifactsDir;

    static bool _runCleanup;

    /// <summary>
    /// Configures the build parameters by hydrating them with values from the Nuke CLI and setting the artifacts
    /// directory.
    /// </summary>
    /// <remarks>This method copies the Nuke CLI parameters into the current context and ensures that the
    /// artifacts directory is set. If the specified artifacts directory does not exist, it defaults to a subdirectory
    /// within the root directory.</remarks>
    protected override void Configure()
    {
        // Copy Nuke CLI parameters
        Parameters.Hydrate(this, verbose: Verbosity == Verbosity.Verbose);

        Parameters.ArtifactsDir = Directory.Exists(ArtifactsDir)
            ? ArtifactsDir
            : Path.Combine(Parameters.RootDirectory, Parameters.ArtifactsDir);
    }

    /// <summary>
    /// Executes the build process for the specified build type and returns the result code.
    /// </summary>
    /// <returns>An integer representing the result code of the build process. A value of 0 typically indicates success, while
    /// non-zero values indicate errors or warnings.</returns>
    public static int Main()
    {
        return Build<Node>(x => x.Build);
    }

    public Target Build => _ => _
        .DependsOn(CopyToArtifacts)
        .Executes(() =>
        {
            Log.Information($"✅ Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });

    public Target CopyToArtifacts => _ => _
        .DependsOn(BuildApplication)
        .Executes(() =>
        {
            Utilities.Node.CopyToArtifacts(Parameters);
        });

    public Target BuildApplication => _ => _
        .DependsOn(GenerateEnvironment)
        .Executes(() =>
        {
            Utilities.Node.Build(Parameters);
        });

    public Target GenerateEnvironment => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            if (!Files.GenerateEnvironmentFile(Parameters.Config.AppEnvMapFilePath, Parameters.Config.AppEnvFilePath))
            {
                Assert.Fail($"❌ App Env File Missing Values, Check {Parameters.Config.AppEnvMapFile}");
            }
        });

    public Target Clean => _ => _
        .DependsOn(Setup)
        .Executes(() =>
        {
            if (Directory.Exists(Parameters.ArtifactsDir))
            {
                Directory.Delete(Parameters.ArtifactsDir, true);

                Log.Information($"🧹 Cleaned Artifacts Directory");
            }

            Directory.CreateDirectory(Parameters.ArtifactsDir);
        });
}