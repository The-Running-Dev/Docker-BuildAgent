using System;
using System.IO;

using Serilog;
using Nuke.Common;
using Newtonsoft.Json;
using Nuke.Common.Tooling;

using Entities;

namespace Utilities;

/// <summary>
/// Retrieves the version information for a project located in the specified root directory.
/// </summary>
public static class Common
{
    /// <summary>
    /// Retrieves the version information for a project located in the specified root directory.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the project for which to retrieve version information.</param>
    /// <returns>A <see cref="VersionInfo"/> object containing the version details of the project. When the directory is
    /// not a Git repository at all, returns a default <see cref="VersionInfo"/> with version set to "0.0.0" and logs a
    /// warning.</returns>
    /// <exception cref="Exception">GitVersion failed or returned no version. The build fails rather than publish a
    /// substituted version (I1: one version value goes to every sink).</exception>
    public static VersionInfo GetVersion(string rootDirectory) => GetVersion(rootDirectory, RunGitVersion);

    internal static VersionInfo GetVersion(string rootDirectory, Func<string, string> runGitVersion)
    {
        // A worktree or a submodule has a `.git` file pointing at the real repository, not a directory.
        var gitPath = Path.Combine(rootDirectory, ".git");

        if (!Directory.Exists(gitPath) && !File.Exists(gitPath))
        {
            Log.Warning("[WARN] {RootDirectory} is not a Git Repository. Using Version 0.0.0.", rootDirectory);

            return new VersionInfo
            {
                Version = "0.0.0",
                FullVersion = "0.0.0",
                Date = "",
                Hash = ""
            };
        }

        var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(runGitVersion(rootDirectory));

        if (string.IsNullOrWhiteSpace(versionInfo?.Version))
        {
            Assert.Fail("[ERROR] Failed to Get Version from GitVersion.");
        }

        return versionInfo;
    }

    private static string RunGitVersion(string rootDirectory)
    {
        var process = ProcessTasks.StartProcess("dotnet-gitversion", "/output json", rootDirectory, logOutput: false, logInvocation: false);
        process.AssertZeroExitCode();

        return process.Output.StdToText();
    }
}