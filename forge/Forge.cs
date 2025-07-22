using System;
using System.IO;
using System.Text;
using System.CommandLine;

using Serilog;
using Nuke.Common;

using Utilities;
using Extensions;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for different types of projects, such as Docker, Node, or GitHub.
/// </summary>
/// <remarks>The <see cref="Forge"/> class provides a command-line interface to execute different build processes
/// based on the specified build type. It supports Docker and Node builds, and can be extended to include additional
/// build types. The class also includes targets for building and updating the changelog.</remarks>
public class Forge : Base<ForgeParams, DiscordNotifications>
{
    [Parameter("Changelog generation source: tag name (e.g., 'v0.1.0'), 'start' for entire history, or empty for last tag")]
    public readonly string ChangeLogFrom;

    /// <summary>
    /// Configures the current instance by hydrating it with Nuke CLI parameters.
    /// </summary>
    /// <remarks>This method copies command-line interface parameters into the current instance, enabling the
    /// use of these parameters within the application. The hydration process is performed with verbose output to
    /// provide detailed information about the parameters being copied.</remarks>
    protected override void Configure()
    {
        // Copy Nuke CLI parameters
        Parameters.Hydrate(this, verbose: Verbosity == Verbosity.Verbose);
        
        // Copy changelog parameter
        Parameters.ChangeLogFrom = ChangeLogFrom;
    }

    public static int Main(string[] args)
    {
        Option<string> buildTypeOption = new("--type")
        {
            Description = "The Build Type to Run."
        };

        Option<string> rootOption = new("--root")
        {
            Description = "Root Directory (default: Current Directory)"
        };

        Option<string> changelogFromOption = new("--changelog-from")
        {
            Description = "Changelog source: tag name (e.g., 'v0.1.0'), 'start' for entire history, or empty for last tag"
        };

        RootCommand rootCommand = new("");
        rootCommand.Options.Add(buildTypeOption);
        rootCommand.Options.Add(rootOption);
        rootCommand.Options.Add(changelogFromOption);

        var parseResult = rootCommand.Parse(args);

        var buildType = parseResult.GetValue(buildTypeOption) is { } parsedBuildType
            ? parsedBuildType
            : "default";
        var rootDirectory = parseResult.GetValue(rootOption) is { } parsedRoot
            ? parsedRoot
            : Environment.CurrentDirectory;

        // Set environment variables for NUKE parameter binding
        // This allows the [Parameter] attributes to pick up the values
        var changelogFrom = parseResult.GetValue(changelogFromOption);

        if (changelogFrom != null)
        {
            Environment.SetEnvironmentVariable("ChangeLogFrom", changelogFrom);
            Console.WriteLine($"🔧 Set ChangeLogFrom = {changelogFrom}");
        }

        Console.WriteLine("╬════════════════════");
        Console.WriteLine($"║ 🔧 Forge: {buildType}");
        
        if (changelogFrom != null)
        {
            Console.WriteLine($"║ 📝 Changelog from: {changelogFrom}");
        }
        Console.WriteLine("╬════════════════════");

        switch (buildType.ToLowerInvariant())
        {
            case "docker":
                return Docker.Main();
            case "node":
                return Node.Main();
            default:
                return Build<Forge>(x => x.Build);
        }
    }

    public Target Build => _ => _
        .DependsOn(ChangeLog)
        .Executes(() =>
        {
        });

    Target ChangeLog => _ => _
        .DependsOn(Setup)
        .Executes(() =>
        {
            var changelogPath = "CHANGELOG.md";
            
            // Determine the starting point for changelog generation
            string fromTag = null;
            string changelogSource = Parameters.ChangeLogFrom?.Trim() ?? "";
            
            if (changelogSource.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                // Generate from entire history
                fromTag = "";
                Log.Information("🔄 Generating changelog from entire git history...");
            }
            else if (!string.IsNullOrEmpty(changelogSource))
            {
                // Generate from specific tag
                fromTag = changelogSource;
                Log.Information($"🔄 Generating changelog from tag: {fromTag}...");
            }
            else
            {
                // Default: from last tag (null means use default behavior)
                Log.Information("🔄 Generating changelog from last tag...");
            }
            
            var changeLog = Git.GenerateChangeLog(fromTag);

            if (string.IsNullOrEmpty(changeLog))
            {
                Log.Information("ℹ️ No commits found for changelog generation.");
                return;
            }

            var today = DateTime.UtcNow.ToString("yyyy.MM.dd");
            var changelogBuilder = new StringBuilder();
            
            // If generating from start or specific tag, don't add today's header since GenerateChangeLog already adds one
            if (!changelogSource.Equals("start", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(changelogSource))
            {
                changelogBuilder.AppendLine($"## {today}\n");
                changelogBuilder.AppendLine(changeLog);
            }
            else
            {
                changelogBuilder.AppendLine(changeLog);
            }

            // Append the rest of the old changelog (if any and not generating from start)
            if (!changelogSource.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                var oldChangelog = File.Exists(changelogPath) ? File.ReadAllText(changelogPath) : "";
                if (!string.IsNullOrEmpty(oldChangelog))
                {
                    changelogBuilder.AppendLine(oldChangelog);
                }
            }

            File.WriteAllText(changelogPath, changelogBuilder.ToString());
            
            Log.Information($"✅ Changelog generated and saved to {changelogPath}");
        });
}