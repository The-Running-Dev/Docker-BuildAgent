using Nuke.Common;

using Entities;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for generating changelogs from Git history.
/// </summary>
/// <remarks>
/// This class extends the <see cref="Base{TParams, TNotifications}"/> class to provide changelog generation
/// functionality. It analyzes Git commit history based on the provided configuration and generates a formatted
/// changelog file.
/// 
/// <para><strong>Build Target Dependencies (in execution order):</strong></para>
/// <list type="number">
/// <item><description><c>Setup</c> - Base setup (from Base class)</description></item>
/// <item><description><c>GenerateChangeLog</c> - Generate and save changelog</description></item>
/// <item><description><c>Build</c> - Final target that logs completion</description></item>
/// </list>
/// </remarks>
public class Forge : Base<ForgeParams, DiscordNotifications>
{
    [Parameter("The source of the change log")]
    public readonly string? ChangeLogSource;

    /// <summary>
    /// Configures the current instance by hydrating parameters and setting up the change log configuration.
    /// </summary>
    /// <remarks>This method copies the Nuke CLI parameters to the current instance and initializes the change
    /// log configuration based on the specified change log source. It logs the change log source information if
    /// verbosity is set to verbose.</remarks>
    protected override void Configure()
    {
        // Copy Nuke CLI parameters
        Parameters.Hydrate(this, verbose: Verbosity == Verbosity.Verbose);
        Parameters.ChangeLogConfig = ChangeLogConfig.FromString(ChangeLogSource);
    }
    
    /// <summary>
    /// Serves as the entry point for the application.
    /// </summary>
    /// <returns>An integer representing the exit code of the application. Typically, a return value of 0 indicates success.</returns>
    public static int Main()
    {
        return Build<Forge>(x => x.Build);
    }
    
    /// <summary>
    /// Gets the build target that depends on the GenerateChangeLog target and executes the build process.
    /// </summary>
    /// <remarks>This target logs the completion of the build process for changelog generation.</remarks>
    public Target Build => _ => _
        .DependsOn(GenerateChangeLog)
        .Executes(() =>
        {
            Logger.Ok($"Build Complete (Forge: {GetType().Name}, Target: {nameof(Build)})");
        });

    /// <summary>
    /// Gets the target that generates and saves the changelog to a specified file.
    /// </summary>
    /// <remarks>This target depends on the <c>Setup</c> target and executes the process of writing the
    /// changelog to a file named <c>CHANGELOG.md</c> using the specified configuration parameters.</remarks>
    public Target GenerateChangeLog => _ => _
        .DependsOn(Setup)
        .Executes(() =>
        {
            var changelogPath = "CHANGELOG.md";

            GitService.WriteChangeLog(changelogPath, Parameters.ChangeLogConfig);

            Logger.Ok($"Changelog Saved: {changelogPath}");
        });
}