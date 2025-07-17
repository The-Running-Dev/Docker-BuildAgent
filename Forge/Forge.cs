using System;
using System.CommandLine;
using System.IO;
using System.Text;

using Serilog;
using Nuke.Common;

using Utilities;
using Parameters;
using Notifications;

/// <summary>
/// Represents a build process for different types of projects, such as Docker, Node, or GitHub.
/// </summary>
/// <remarks>The <see cref="Forge"/> class provides a command-line interface to execute different build processes
/// based on the specified build type. It supports Docker and Node builds, and can be extended to include additional
/// build types. The class also includes targets for building and updating the changelog.</remarks>
public class Forge : BaseBuild<ForgeParams, DiscordNotifications>
{
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

        RootCommand rootCommand = new("");
        rootCommand.Options.Add(buildTypeOption);
        rootCommand.Options.Add(rootOption);

        var parseResult = rootCommand.Parse(args);

        var buildType = parseResult.GetValue(buildTypeOption) is { } parsedBuildType
            ? parsedBuildType
            : "default";
        var rootDirectory = parseResult.GetValue(rootOption) is { } parsedRoot
            ? parsedRoot
            : Environment.CurrentDirectory;

        Console.WriteLine("╬════════════════════");
        Console.WriteLine($"║ 🔧 Forge: {buildType}");
        Console.WriteLine("╬════════════════════");

        switch (buildType.ToLowerInvariant())
        {
            case "docker":
                return DockerBuild.Main();
            case "node":
                return NodeBuild.Main();
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
            var changeLog = Git.ChangeLog;

            if (string.IsNullOrEmpty(changeLog))
            {
                Log.Information("No New Commits Since Last Tag...");

                return;
            }

            var today = DateTime.UtcNow.ToString("yyyy.MM.dd");
            var changelogBuilder = new StringBuilder();
            changelogBuilder.AppendLine($"## {today}\n");

            // Append the rest of the old changelog (if any)
            var oldChangelog = File.Exists(changelogPath) ? File.ReadAllText(changelogPath) : "";
            changelogBuilder.AppendLine(oldChangelog);

            File.WriteAllText(changelogPath, changelogBuilder.ToString());
        });
}