using System;
using System.IO;
using Serilog;
using Nuke.Common;

using Utilities;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for Docker images, including configuration and execution of build, tag, push, and release
/// targets.
/// </summary>
/// <remarks>The <see cref="DockerBuild"/> class is responsible for managing the lifecycle of a Docker image build
/// process.  It includes setting up parameters such as repository details and image tags, and executing various build
/// targets  like creating a GitHub release, pushing images, and tagging them. The class extends <see
/// cref="BaseBuild{TParams, TNotifications}"/>  with specific parameters for Docker and notifications for
/// Discord.</remarks>
public class DockerBuild : BaseBuild<DockerParams, DiscordNotifications>
{
    [Parameter("Templates Directory")]
    public readonly string TemplatesDir;

    [Parameter("Docker Repository")]
    public readonly string Repository;

    [Parameter("Repository User")]
    public readonly string RepositoryUser;

    [Parameter("Repository Token for Pushing Images")]
    [Secret]
    public readonly string RepositoryToken;

    [Parameter("Image Tag")]
    public readonly string ImageTag;
    
    [Parameter("Dockerfile Path")]
    public readonly string DockerFile = "Dockerfile";

    [Parameter("Dockerfile Path")]
    public readonly bool CreateGitHubRelease = false;

    /// <summary>
    /// Configures the specified <see cref="DockerParams"/> instance with the necessary parameters for Docker
    /// operations.
    /// </summary>
    /// <remarks>This method sets various properties on the <paramref name="p"/> parameter, including the
    /// templates directory, repository information, user credentials, and Docker tags. It also verifies that the
    /// <c>ImageTag</c> property is not null or whitespace, throwing an assertion failure if it is.</remarks>
    /// <param name="p">The <see cref="DockerParams"/> instance to configure.</param>
    protected override void Configure(DockerParams p)
    {
        if (string.IsNullOrWhiteSpace(ImageTag))
        {
            Assert.Fail("❌ ImageTag is Required.");
        }

        p.TemplatesDir = Directory.Exists(TemplatesDir) ? TemplatesDir : Path.Combine(Environment.CurrentDirectory, "templates");
        p.Repository = Repository;
        p.User = RepositoryUser;
        p.Token = RepositoryToken;
        p.Tags =
        [
            $"{Repository}/{ImageTag}:latest",
            $"{Repository}/{ImageTag}:{p.Version}"
        ];
        p.DockerFile = DockerFile;
        p.CreateGitHubRelease = CreateGitHubRelease;
    }

    /// <summary>
    /// Executes the build process for the specified build type and returns the exit code.
    /// </summary>
    /// <returns>An integer representing the exit code of the build process. A value of 0 typically indicates success, while any
    /// other value indicates an error.</returns>
    public static int Main()
    {
        return Build<DockerBuild>(x => x.Build);
    }

    public Target Build => _ => _
        .DependsOn(CreateRelease)
        .Executes(() =>
        {
            Log.Information($"✅ Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });

    Target CreateRelease => _ => _
        .DependsOn(Push)
        .OnlyWhenDynamic(() =>
            CreateGitHubRelease &&
            !IsLocalBuild &&
            !DryRun &&
            !string.IsNullOrWhiteSpace(GitRepository?.HttpsUrl) &&
            !string.IsNullOrWhiteSpace(RepositoryToken))
        .Executes(async () =>
        {
            await GitHub.CreateRelease(Parameters);
        });
    
    public Target Push => _ => _
        .DependsOn(Tag)
        .OnlyWhenDynamic(() => ForcePush || (!IsLocalBuild && !DryRun))
        .Executes(() =>
        {
            if (string.IsNullOrWhiteSpace(RepositoryToken))
            {
                Assert.Fail("❌ Repository Token is Not Set.");
            }

            Docker.Push(Parameters);
            
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
            Docker.Build(Parameters);
        });
}