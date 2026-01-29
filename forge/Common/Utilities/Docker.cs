using System.IO;
using System.Linq;

using Serilog;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;

using Extensions;
using Parameters;

namespace Utilities;

/// <summary>
/// Provides methods for managing Docker operations such as login, build, tag, and push.
/// </summary>
/// <remarks>This static class offers a set of methods to facilitate common Docker tasks using specified
/// parameters. It abstracts the underlying Docker command invocations, providing a simplified interface for Docker
/// operations.</remarks>
public static class Docker
{
    /// <summary>
    /// Logs into a Docker registry using the specified parameters.
    /// </summary>
    /// <remarks>This method configures the login process by setting the server, username, and password based
    /// on the provided <paramref name="p"/>. The server is derived from the repository URL by removing any path
    /// components.</remarks>
    /// <param name="p">The parameters required for logging into the Docker registry, including the repository, user, and token.</param>
    public static void Login(DockerParams p)
    {
        var server = GetRegistryServerForLogin(p.RegistryUrl);

        DockerTasks.DockerLogin(s => s
            .DisableProcessInvocationLogging()
            .SetServer(server)
            .SetUsername(p.RegistryUser)
            .SetPassword(p.RegistryToken));
    }

    internal static string GetRegistryServerForLogin(string registryUrl)
    {
        return registryUrl.GetRegistryServer();
    }

    /// <summary>
    /// Builds a Docker image using the specified parameters.
    /// </summary>
    /// <remarks>This method constructs a Docker image by locating the Dockerfile in the specified root
    /// directory and applying the provided tags.  It logs the build process information based on the verbosity level
    /// set in the parameters.</remarks>
    /// <param name="p">The parameters used to configure the Docker build, including the root directory, Dockerfile path, tags, and
    /// verbosity level.</param>
    public static void Build(DockerParams p)
    {
        var dockerFile = Path.Combine(p.RootDirectory, p.DockerFile);
        var latestTag = p.Tags.FirstOrDefault(x => x.Contains("latest"));
        var versionTag = p.Tags.FirstOrDefault(x => !x.Contains("latest"));
        if (string.IsNullOrWhiteSpace(latestTag) || string.IsNullOrWhiteSpace(versionTag))
        {
            Assert.Fail("Docker tags must include both a 'latest' tag and a version tag.");
        }

        if (!File.Exists(dockerFile) && Directory.Exists(p.TemplatesDir))
        {
            Log.Warning($"Dockerfile not Found, Using a Template...");
            
            var appType = Node.DetectApplicationType(p.RootDirectory);
            var templateDockerFile = Path.Combine(p.TemplatesDir, $"Dockerfile.{appType}");

            if (!File.Exists(templateDockerFile))
            {
                Assert.Fail($"No Dockerfile Template Exists {dockerFile}, Aborting...");
            }

            Log.Warning($"Using Dockerfile Template: {templateDockerFile}...");

            File.Copy(templateDockerFile, dockerFile);
        }

        Log.Information($"Building {dockerFile}...");

        DockerTasks.DockerBuild(s => s
            .SetPath(p.RootDirectory)
            .SetProcessLogger((type, text) =>
            {
                if (p.Verbosity == Verbosity.Verbose) Log.Information(text);
            })
            .DisableProcessInvocationLogging()
            .SetFile(dockerFile)
            .SetTag($"{latestTag}"));

        Log.Information($"🐳 Tagged: {latestTag}");

        Tag(p);
    }

    /// <summary>
    /// Pushes Docker images to a remote repository using the specified parameters.
    /// </summary>
    /// <remarks>This method logs into the Docker registry and pushes images tagged as "latest" and with a
    /// version-specific tag. Ensure that the <paramref name="p"/> parameter contains valid tags for the operation to
    /// succeed.</remarks>
    /// <param name="p">The parameters used for the Docker push operation, including tags and version information.</param>
    public static void Push(DockerParams p)
    {
        Login(p);

        var latestTag = p.Tags.FirstOrDefault(x => x.Contains("latest"));
        var versionTag = p.Tags.FirstOrDefault(x => !x.Contains("latest"));

        DockerTasks.DockerPush(s => s
            .DisableProcessInvocationLogging()
            .SetName($"{latestTag}"));

        DockerTasks.DockerPush(s => s
            .DisableProcessInvocationLogging()
            .SetName($"{versionTag}"));

        Log.Information($"📤 Pushed Docker Images: {p.Version}, latest");
    }

    /// <summary>
    /// Tags a Docker image with a version-specific tag based on the provided parameters.
    /// </summary>
    /// <remarks>This method selects the first tag containing "latest" as the source image and the first tag
    /// not containing "latest" as the target image. It then uses these tags to create a new Docker image tag. The
    /// operation is logged upon completion.</remarks>
    /// <param name="p">The parameters containing the list of tags to be used for tagging the Docker image.</param>
    public static void Tag(DockerParams p)
    {
        var latestTag = p.Tags.FirstOrDefault(x => x.Contains("latest"));
        var versionTag = p.Tags.FirstOrDefault(x => !x.Contains("latest"));

        DockerTasks.DockerTag(s => s
            .DisableProcessInvocationLogging()
            .SetSourceImage($"{latestTag}")
            .SetTargetImage($"{versionTag}"));

        Log.Information($"🐳 Tagged: {versionTag}");
    }
}