using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Microsoft.Extensions.Logging;

using Extensions;
using Parameters;

namespace Services;

/// <summary>
/// Provides methods for managing Docker operations such as login, build, tag, and push.
/// </summary>
/// <remarks>
/// This interface defines Docker operations that abstract the underlying Docker command invocations, 
/// providing a simplified interface for Docker operations with dependency injection support.
/// </remarks>
public interface IDockerService
{
    /// <summary>
    /// Logs into a Docker registry using the specified parameters.
    /// </summary>
    /// <remarks>
    /// This method configures the login process by setting the server, username, and password based
    /// on the provided <paramref name="parameters"/>. The server is derived from the repository URL by removing any path
    /// components.
    /// </remarks>
    /// <param name="parameters">The parameters required for logging into the Docker registry, including the repository, user, and token.</param>
    void Login(DockerParams parameters);

    /// <summary>
    /// Builds a Docker image using the specified parameters.
    /// </summary>
    /// <remarks>
    /// This method constructs a Docker image by locating the Dockerfile in the specified root
    /// directory and applying the provided tags. It logs the build process information based on the verbosity level
    /// set in the parameters.
    /// </remarks>
    /// <param name="parameters">The parameters used to configure the Docker build, including the root directory, Dockerfile path, tags, and verbosity level.</param>
    void Build(DockerParams parameters);

    /// <summary>
    /// Pushes Docker images to a remote repository using the specified parameters.
    /// </summary>
    /// <remarks>
    /// This method logs into the Docker registry and pushes images tagged as "latest" and with a
    /// version-specific tag. Ensure that the <paramref name="parameters"/> parameter contains valid tags for the operation to
    /// succeed.
    /// </remarks>
    /// <param name="parameters">The parameters used for the Docker push operation, including tags and version information.</param>
    void Push(DockerParams parameters);

    /// <summary>
    /// Tags a Docker image with a version-specific tag based on the provided parameters.
    /// </summary>
    /// <remarks>
    /// This method selects the first tag containing "latest" as the source image and the first tag
    /// not containing "latest" as the target image. It then uses these tags to create a new Docker image tag. The
    /// operation is logged upon completion.
    /// </remarks>
    /// <param name="parameters">The parameters containing the list of tags to be used for tagging the Docker image.</param>
    void Tag(DockerParams parameters);
}

/// <summary>
/// The Docker commands <see cref="DockerService"/> runs. The default runs the Docker CLI through NUKE; tests
/// substitute one that never reaches a daemon.
/// </summary>
internal interface IDockerCli
{
    void Login(string server, string username, string password);

    void Build(string path, string dockerFile, string tag, Action<string> output);

    void Push(string name);

    void Tag(string source, string target);
}

internal sealed class NukeDockerCli : IDockerCli
{
    public void Login(string server, string username, string password) =>
        DockerTasks.DockerLogin(s => s
            .DisableProcessInvocationLogging()
            .SetServer(server)
            .SetUsername(username)
            .SetPassword(password));

    public void Build(string path, string dockerFile, string tag, Action<string> output) =>
        DockerTasks.DockerBuild(s => s
            .SetPath(path)
            .SetProcessLogger((_, text) => output(text))
            .DisableProcessInvocationLogging()
            .SetFile(dockerFile)
            .SetTag(tag));

    public void Push(string name) =>
        DockerTasks.DockerPush(s => s
            .DisableProcessInvocationLogging()
            .SetName(name));

    public void Tag(string source, string target) =>
        DockerTasks.DockerTag(s => s
            .DisableProcessInvocationLogging()
            .SetSourceImage(source)
            .SetTargetImage(target));
}

/// <summary>
/// Provides methods for managing Docker operations such as login, build, tag, and push.
/// </summary>
/// <remarks>
/// The <see cref="DockerService"/> class includes methods to manage Docker operations such as logging into
/// registries, building images, tagging, and pushing to remote repositories. This service implementation wraps the 
/// static Docker utility methods and provides dependency injection support.
/// </remarks>
public class DockerService : IDockerService
{
    private readonly ILogger<DockerService> _logger;
    
    private readonly INodeService _nodeService;

    private readonly IDockerCli _docker;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockerService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <param name="nodeService">The Node service for detecting application types.</param>
    public DockerService(ILogger<DockerService> logger, INodeService nodeService)
        : this(logger, nodeService, new NukeDockerCli()) { }

    internal DockerService(ILogger<DockerService> logger, INodeService nodeService, IDockerCli docker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nodeService = nodeService ?? throw new ArgumentNullException(nameof(nodeService));
        _docker = docker ?? throw new ArgumentNullException(nameof(docker));
    }

    /// <summary>
    /// Logs into a Docker registry using the specified parameters.
    /// </summary>
    /// <remarks>
    /// This method configures the login process by setting the server, username, and password based
    /// on the provided <paramref name="parameters"/>. The server is derived from the repository URL by removing any path
    /// components.
    /// </remarks>
    /// <param name="parameters">The parameters required for logging into the Docker registry, including the repository, user, and token.</param>
    /// <exception cref="BuildFailureException">The login failed; the build ends with status 6.</exception>
    public void Login(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var server = Regex.Replace(parameters.RegistryUrl, @"/.*$", "");

        try
        {
            _docker.Login(server, parameters.RegistryUser, parameters.RegistryToken);
        }
        catch (Exception)
        {
            // The inner exception is left off: its text can carry the command line, and with it the token.
            throw new BuildFailureException(BuildFailure.Registry, $"Docker Login to {server} Failed");
        }
    }

    /// <summary>
    /// Finds a template Dockerfile by searching through multiple directories in order of priority.
    /// </summary>
    /// <param name="parameters">The parameters containing root directory and template directory information.</param>
    /// <returns>The full path to the template Dockerfile if found, otherwise null.</returns>
    /// <remarks>
    /// Searches for templates in the following order:
    /// 1. User-specified TemplatesDir parameter (if exists)
    /// 2. .github/templates/ in project root
    /// 3. templates/ in project root  
    /// 4. /nuke/templates/ (container fallback)
    /// </remarks>
    private string FindTemplateDockerFile(DockerParams parameters)
    {
        var appType = _nodeService.DetectApplicationType(parameters.RootDirectory);
        var templateFileName = $"Dockerfile.{appType}";
        
        // Define template directories in order of priority
        var templateDirectories = new[]
        {
            parameters.TemplatesDir, // User-specified or configured template directory
            Path.Combine(parameters.RootDirectory, ".github", "templates"), // GitHub convention
            Path.Combine(parameters.RootDirectory, "templates"), // Project root templates
            "/nuke/templates" // Container fallback (for backward compatibility)
        };

        foreach (var templateDir in templateDirectories)
        {
            if (!string.IsNullOrEmpty(templateDir) && Directory.Exists(templateDir))
            {
                var templatePath = Path.Combine(templateDir, templateFileName);
                if (File.Exists(templatePath))
                {
                    _logger.LogInformation("Found template in: {TemplateDirectory}", templateDir);
                    return templatePath;
                }
            }
        }

        // If no template found, log the attempted locations and throw exception
        var searchedLocations = templateDirectories
            .Where(dir => !string.IsNullOrEmpty(dir))
            .Select(dir => $"  - {dir}")
            .ToArray();
            
        _logger.LogError("No Dockerfile template found for application type '{AppType}'. Searched locations:\n{SearchedLocations}", 
            appType, string.Join("\n", searchedLocations));
            
        throw new InvalidOperationException($"No Dockerfile Template exists for application type '{appType}'. " +
            $"Please create a template named '{templateFileName}' in one of the following locations: " +
            $"{string.Join(", ", templateDirectories.Where(d => !string.IsNullOrEmpty(d)))}");
    }

    /// <summary>
    /// Builds a Docker image using the specified parameters.
    /// </summary>
    /// <remarks>
    /// This method constructs a Docker image by locating the Dockerfile in the specified root
    /// directory and applying the provided tags. It logs the build process information based on the verbosity level
    /// set in the parameters. If no Dockerfile exists, it searches for templates in multiple locations:
    /// 1. User-specified TemplatesDir parameter
    /// 2. .github/templates/ in project root
    /// 3. templates/ in project root
    /// 4. /nuke/templates/ (container fallback)
    /// </remarks>
    /// <param name="parameters">The parameters used to configure the Docker build, including the root directory, Dockerfile path, tags, and verbosity level.</param>
    public void Build(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var dockerFile = Path.Combine(parameters.RootDirectory, parameters.DockerFile);
        var latestTag = parameters.Tags.FirstOrDefault(x => x.Contains("latest"));
        var copiedTemplate = false;

        if (!File.Exists(dockerFile))
        {
            var templateDockerFile = FindTemplateDockerFile(parameters);

            _logger.LogWarning("Dockerfile not Found, Using a Template...");
            _logger.LogWarning("Using Dockerfile Template: {TemplateDockerFile}...", templateDockerFile);
            File.Copy(templateDockerFile, dockerFile);
            copiedTemplate = true;
        }

        _logger.LogInformation("Building {DockerFile}...", dockerFile);

        try
        {
            _docker.Build(parameters.RootDirectory, dockerFile, $"{latestTag}", text =>
            {
                if (parameters.Verbosity == Verbosity.Verbose) _logger.LogInformation(text);
            });
        }
        finally
        {
            // The template copy is build input only: never leave it in the consumer's working tree.
            if (copiedTemplate)
            {
                File.Delete(dockerFile);
            }
        }

        _logger.Tag("{LatestTag}", latestTag);

        Tag(parameters);
    }

    /// <summary>
    /// Pushes Docker images to a remote repository using the specified parameters.
    /// </summary>
    /// <remarks>
    /// This method logs into the Docker registry and pushes images tagged as "latest" and with a
    /// version-specific tag. Ensure that the <paramref name="parameters"/> parameter contains valid tags for the operation to
    /// succeed.
    /// </remarks>
    /// <param name="parameters">The parameters used for the Docker push operation, including tags and version information.</param>
    /// <exception cref="BuildFailureException">The login or a push failed; the build ends with status 6.</exception>
    public void Push(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        Login(parameters);

        var latestTag = parameters.Tags.FirstOrDefault(x => x.Contains("latest"));
        var versionTag = parameters.Tags.FirstOrDefault(x => !x.Contains("latest"));

        PushImage($"{latestTag}");

        // No versioned tag means a main push (I15): only `latest` moves.
        if (!string.IsNullOrEmpty(versionTag))
        {
            PushImage(versionTag);
        }

        if (string.IsNullOrEmpty(versionTag))
        {
            _logger.Push("Docker Images: latest");
        }
        else
        {
            _logger.Push("Docker Images: {Version}, latest", parameters.Version);
        }
    }

    /// <summary>
    /// Tags a Docker image with a version-specific tag based on the provided parameters.
    /// </summary>
    /// <remarks>
    /// This method selects the first tag containing "latest" as the source image and the first tag
    /// not containing "latest" as the target image. It then uses these tags to create a new Docker image tag. The
    /// operation is logged upon completion.
    /// </remarks>
    /// <param name="parameters">The parameters containing the list of tags to be used for tagging the Docker image.</param>
    /// <exception cref="BuildFailureException">The daemon refused the tag; the build ends with status 5.</exception>
    public void Tag(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        var latestTag = parameters.Tags.FirstOrDefault(x => x.Contains("latest"));
        var versionTag = parameters.Tags.FirstOrDefault(x => !x.Contains("latest"));

        // No versioned tag means a main push (I15): there is nothing to tag.
        if (string.IsNullOrEmpty(versionTag))
        {
            return;
        }

        try
        {
            _docker.Tag($"{latestTag}", versionTag);
        }
        catch (Exception ex)
        {
            throw new BuildFailureException(BuildFailure.DockerDaemon, $"Docker Tag {versionTag} Failed", ex);
        }

        _logger.Tag("{VersionTag}", versionTag);
    }

    private void PushImage(string name)
    {
        try
        {
            _docker.Push(name);
        }
        catch (Exception ex)
        {
            throw new BuildFailureException(BuildFailure.Registry, $"Docker Push {name} Failed", ex);
        }
    }
}