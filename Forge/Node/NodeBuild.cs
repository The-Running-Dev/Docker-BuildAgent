using System.IO;

using Serilog;
using Nuke.Common;

using Utilities;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for a Node.js application, providing targets for building, cleaning, and managing
/// artifacts.
/// </summary>
/// <remarks>This class extends <see cref="BaseBuild{NodeParams, DiscordNotifications}"/> to define specific build
/// targets and configurations for a Node.js project. It includes targets for building the application, copying
/// artifacts, and cleaning up directories.</remarks>
public class NodeBuild : BaseBuild<NodeParams, DiscordNotifications>
{
    [Parameter("The Artifacts Directory")]
    public readonly string ArtifactsDirectory;

    static bool _runCleanup;

    /// <summary>
    /// Configures the specified <see cref="NodeParams"/> instance with the necessary parameters.
    /// </summary>
    /// <remarks>The method sets the <see cref="NodeParams.ArtifactsDirectory"/> property to the value of the 
    /// <see cref="ArtifactsDirectory"/> field. If <see cref="ArtifactsDirectory"/> is null, empty, or consists  only of
    /// white-space characters, the method will fail with an assertion.</remarks>
    /// <param name="p">The <see cref="NodeParams"/> instance to configure. This parameter cannot be null.</param>
    protected override void Configure(NodeParams p)
    {
        if (string.IsNullOrWhiteSpace(ArtifactsDirectory))
        {
            Assert.Fail("❌ Artifacts Directory is Required.");
        }

        p.ArtifactsDirectory = ArtifactsDirectory;
    }

    /// <summary>
    /// Executes the build process for the specified build type and returns the result code.
    /// </summary>
    /// <returns>An integer representing the result code of the build process. A value of 0 typically indicates success, while
    /// non-zero values indicate errors or warnings.</returns>
    public static int Main()
    {
        return Build<NodeBuild>(x => x.Build);
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
            Node.CopyToArtifacts(Parameters);
        });

    public Target BuildApplication => _ => _
        .DependsOn(GenerateEnvironment)
        .Executes(() =>
        {
            Node.Build(Parameters);
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
            if (Directory.Exists(Parameters.ArtifactsDirectory))
            {
                Directory.Delete(Parameters.ArtifactsDirectory, true);

                Log.Information($"🧹 Cleaned Artifacts Directory");
            }

            Directory.CreateDirectory(Parameters.ArtifactsDirectory);
        });
}