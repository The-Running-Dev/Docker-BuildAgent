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
    /// Retrieves the version information from the specified root directory using GitVersion.
    /// </summary>
    /// <param name="rootDirectory">The root directory from which to retrieve the version information.</param>
    /// <returns>A <see cref="VersionInfo"/> object containing the version details.</returns>
    public static VersionInfo GetVersion(string rootDirectory)
    {
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