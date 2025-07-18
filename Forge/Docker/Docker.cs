using System.IO;

using Serilog;
using Nuke.Common;

using Utilities;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for Docker images, including configuration and execution of build, tag, push, and release
/// targets.
/// </summary>
/// <remarks>The <see cref="Docker"/> class is responsible for managing the lifecycle of a Docker image build
/// process.  It includes setting up parameters such as repository details and image tags, and executing various build
/// targets  like creating a GitHub release, pushing images, and tagging them. The class extends <see
/// cref="Base{TParams,TNotifications}"/>  with specific parameters for Docker and notifications for
/// Discord.</remarks>
public class Docker : Base<DockerParams, DiscordNotifications>
{
    [Parameter("Templates Directory for Dockerfile templates")]
    public readonly string TemplatesDir;

    [Parameter("Docker Registry for pushing images")]
    public readonly string RegistryUrl;

    [Parameter("Registry Registry user for pushing images")]
    public readonly string RegistryUser;

    [Parameter("Registry Registry token for pushing images")]
    [Secret]
    public readonly string RegistryToken;

    [Parameter("Tag for the Docker Image")]
    public readonly string ImageTag;

    [Parameter("Dockerfile to use for building the image")]
    public readonly string DockerFile;

    [Parameter("Should a GitHub release be created")]
    public readonly bool CreateGitHubRelease;

    /// <summary>
    /// Configures the current instance by setting up necessary parameters and paths.
    /// </summary>
    /// <remarks>This method initializes the parameters required for the operation, including setting the
    /// templates directory and constructing image tags with the specified registry URL. It ensures that the parameters
    /// are hydrated with verbose output and adjusts paths based on the existence of directories.</remarks>
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
    }

    /// <summary>
    /// Executes the build process for the specified build type and returns the exit code.
    /// </summary>
    /// <returns>An integer representing the exit code of the build process. A value of 0 typically indicates success, while any
    /// other value indicates an error.</returns>
    public static int Main()
    {
        return Build<Docker>(x => x.Build);
    }

    /// <summary>
    /// Gets the build target that depends on the release creation process and executes the build actions.
    /// </summary>
    public Target Build => _ => _
        .DependsOn(CreateRelease)
        .Executes(() =>
        {
            Log.Information($"✅ Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });

    Target CreateRelease => _ => _
        .DependsOn(Push)
        .OnlyWhenDynamic(() =>
            Parameters.CreateGitHubRelease &&
            !IsLocalBuild &&
            !DryRun &&
            !string.IsNullOrWhiteSpace(GitRepository?.HttpsUrl) &&
            !string.IsNullOrWhiteSpace(RegistryToken))
        .Executes(async () =>
        {
            await GitHub.CreateRelease(Parameters);
        });

    public Target Push => _ => _
        .DependsOn(Tag)
        .OnlyWhenDynamic(() => ForcePush || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(RegistryToken))
            {
                Assert.Fail("❌ Repository RegistryToken is Not Set.");
            }

            Utilities.Docker.Push(Parameters);

            Log.Information($"📤 Pushed Docker Images: {Parameters.Version.Version}, latest");
        });

    public Target Tag => _ => _
        .DependsOn(Image)
        .OnlyWhenDynamic(() => ForcePush || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            Git.CreateTag(Parameters.Version.ToString());
        });

    public Target Image => _ => _
        .DependsOn(Setup)
        .Executes(() =>
        {
            Utilities.Docker.Build(Parameters);
        });
}