using System;
using System.IO;
using System.Text;

using Nuke.Common;
using Newtonsoft.Json;
using Nuke.Common.Tooling;

using Entities;

namespace Utilities;

/// <summary>
/// Provides common utility methods for versioning and changelog management.
/// </summary>
public static class Common
{
    /// <summary>
    /// Retrieves the version information for a project located in the specified root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the project for which to retrieve version information. This directory must contain a .git
    /// folder.</param>
    /// <returns>A <see cref="VersionInfo"/> object containing the version details of the project.  If the .git directory is not
    /// found, returns a default <see cref="VersionInfo"/> with version set to "0.0.0".</returns>
    public static VersionInfo GetVersion(string rootDirectory)
    {
        var gitDir = Path.Combine(rootDirectory, ".git");

        if (!Directory.Exists(gitDir))
        {
            // Return a default VersionInfo if .git is missing
            return new VersionInfo
            {
                Version = "0.0.0",
                FullVersion = "0.0.0",
                Date = "",
                Hash = ""
            };
        }

        var process = ProcessTasks.StartProcess("dotnet-gitversion", "/output json", rootDirectory, logOutput: false, logInvocation: false);
        process.AssertZeroExitCode();

        var output = process.Output.StdToText();
        var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(output);

        if (string.IsNullOrWhiteSpace(versionInfo?.Version))
        {
            Assert.Fail("❌ Failed to Get Version from GitVersion.");
        }

        return versionInfo;
    }

    /// <summary>
    /// Appends the current date and the specified change log entry to the existing change log file.
    /// </summary>
    /// <remarks>If the specified change log file does not exist, a new file will be created. The current date
    /// is formatted as "yyyy.MM.dd".</remarks>
    /// <param name="changeLogPath">The file path of the change log to which the entry will be written. Must not be null or empty.</param>
    /// <param name="changeLog">The change log entry to append. This entry will be prefixed with the current date.</param>
    public static void WriteChangeLog(string changeLogPath, string changeLog)
    {
        var today = DateTime.UtcNow.ToString("yyyy.MM.dd");
        var changelogBuilder = new StringBuilder();

        changelogBuilder.AppendLine($"## {today}\n");

        var oldChangelog = File.Exists(changeLogPath) ? File.ReadAllText(changeLogPath) : "";

        changelogBuilder.AppendLine(oldChangelog);

        File.WriteAllText(changeLogPath, changelogBuilder.ToString());
    }
}