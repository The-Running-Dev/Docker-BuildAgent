using System.Collections.Generic;

namespace Parameters;

/// <summary>
/// Represents the parameters required for building and managing Docker images.
/// </summary>
/// <remarks>This class provides properties to specify various settings related to Docker image creation, such as
/// the Dockerfile path, image tags, and registry credentials. It also includes options for creating a GitHub release
/// associated with the Docker image.</remarks>
public class DockerParams : ForgeParams
{
    /// <summary>
    /// Gets or sets the directory path where Dockerfile template files are stored.
    /// </summary>
    public string TemplatesDir { get; set; } = "/nuke/templates";

    /// <summary>
    /// Gets or sets the path to the Dockerfile used for building Docker images.
    /// </summary>
    public string DockerFile { get; set; } = "Dockerfile";

    /// <summary>
    /// Gets or sets the tag associated with the container image.
    /// </summary>
    public string ImageTag { get; set; } = "container-app";

    /// <summary>
    /// Gets or sets the tag associated with the container image.
    /// </summary>
    public List<string> Tags { get; set; }

    /// <summary>
    /// Gets or sets the registry URL where to push the Docker image.
    /// </summary>
    public string RegistryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the registry user.
    /// </summary>
    public string RegistryUser { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the registry token.
    /// </summary>
    public string RegistryToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a GitHub release should be created.
    /// </summary>
    public bool CreateGitHubRelease { get; set; } = false;

    /// <summary>
    /// Gets or sets the release tag for the current version.
    /// </summary>
    public string ReleaseTag { get; set; } = "v0.0.0";
}