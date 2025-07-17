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
    public BuildConfig Config { get; set; }

    public string RootDirectory { get; set; }

    public string RepositoryUrl { get; set;}

    public VersionInfo Version { get; set; }

    public bool Notifications { get; set; }

    public bool ForceNotifications { get; set; }

    public string NotificationsWebHookUrl { get; set; }

    public bool ForcePush { get; set; }

    public bool DryRun { get; set; }

    public Verbosity Verbosity { get; set; }
}