namespace Parameters;

/// <summary>
/// Represents the parameters required for building Node.js applications and packaging them as Docker images.
/// </summary>
/// <remarks>This class extends DockerParams to support a build process that first builds a Node.js application
/// and then packages it into a Docker image. It inherits all Docker-related functionality while adding
/// Node.js-specific parameters for managing artifacts.</remarks>
public class NodeInDockerParams : DockerParams
{
    /// <summary>
    /// Gets or sets the directory path where Node.js build artifacts are stored.
    /// </summary>
    /// <remarks>This directory is used to store the output from the Node.js build process before
    /// it gets packaged into the Docker image.</remarks>
    public string ArtifactsDir { get; set; } = "artifacts";

    /// <summary>
    /// Converts this NodeInDockerParams to a NodeParams instance for use with NodeService operations.
    /// </summary>
    /// <returns>A NodeParams instance with equivalent values from the base ForgeParams properties.</returns>
    public NodeParams ToNodeParams()
    {
        return new NodeParams
        {
            ArtifactsDir = this.ArtifactsDir,
            Config = this.Config,
            RootDirectory = this.RootDirectory,
            RepositoryUrl = this.RepositoryUrl,
            Version = this.Version,
            Notifications = this.Notifications,
            ForceNotifications = this.ForceNotifications,
            NotificationsWebHookUrl = this.NotificationsWebHookUrl,
            ForcePush = this.ForcePush,
            DryRun = this.DryRun,
            ChangeLogConfig = this.ChangeLogConfig,
            Verbosity = this.Verbosity
        };
    }
}