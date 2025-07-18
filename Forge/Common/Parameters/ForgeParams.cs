using Nuke.Common;

using Entities;

namespace Parameters;

/// <summary>
/// Represents the parameters required for configuring a build process in a forge environment.
/// </summary>
/// <remarks>This class encapsulates various settings such as build configuration, repository details, 
/// notification preferences, and verbosity level, which are used to control the behavior of the  build process. It
/// provides options to enable or disable notifications, force certain actions,  and perform a dry run for testing
/// purposes.</remarks>
public class ForgeParams
{
    /// <summary>
    /// Gets or sets the build configuration settings.
    /// </summary>
    public BuildConfig Config { get; set; }

    public string RootDirectory { get; set; }

    /// <summary>
    /// Gets or sets the URL of the repository.
    /// </summary>
    public string RepositoryUrl { get; set;}

    /// <summary>
    /// Gets or sets the version information for the current application.
    /// </summary>
    public VersionInfo Version { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether notifications are enabled.
    /// </summary>
    public bool Notifications { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether notifications should be forcibly sent, regardless of other conditions.
    /// </summary>
    public bool ForceNotifications { get; set; }

    /// <summary>
    /// Gets or sets the URL for the notifications webhook.
    /// </summary>
    public string NotificationsWebHookUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the push operation should be forced.
    /// </summary>
    public bool ForcePush { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the operation should be executed in dry-run mode.
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets the verbosity level for logging output.
    /// </summary>
    public Verbosity Verbosity { get; set; }
}