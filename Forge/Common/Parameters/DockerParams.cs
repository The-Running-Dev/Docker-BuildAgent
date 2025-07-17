using System.Collections.Generic;

namespace Parameters;

/// <summary>
/// Represents the parameters required for Docker operations, including building and pushing Docker images.
/// </summary>
/// <remarks>This class provides properties to specify paths, tags, and credentials necessary for Docker image
/// management. It includes options for specifying the Dockerfile location, image tags, and repository details.
/// Additionally, it supports creating a GitHub release associated with the Docker image.</remarks>
public class DockerParams : ForgeParams
{
    /// <summary>
    /// Gets or sets the directory path where Dockerfile template files are stored.
    /// </summary>
    public string TemplatesDir { get; set; }

    /// <summary>
    /// Gets or sets the path to the Dockerfile used for building Docker images.
    /// </summary>
    public string DockerFile { get; set; }

    /// <summary>
    /// Gets or sets the tag associated with the image.
    /// </summary>
    public List<string> Tags { get; set; }

    /// <summary>
    /// Gets or sets the repository where to push the Docker image.
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// Gets or sets the repository username.
    /// </summary>
    public string User { get; set; }

    /// <summary>
    /// Gets or sets the repository token.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a GitHub release should be created.
    /// </summary>
    public bool CreateGitHubRelease { get; set; }
}