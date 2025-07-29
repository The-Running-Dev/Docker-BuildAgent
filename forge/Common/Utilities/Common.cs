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

        try
        {
            var process = ProcessTasks.StartProcess("dotnet-gitversion", "/output json", rootDirectory, logOutput: false, logInvocation: false);
            process.AssertZeroExitCode();

            var output = process.Output.StdToText();
            var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(output);

            if (string.IsNullOrWhiteSpace(versionInfo?.Version))
            {
                Assert.Fail("[ERROR] Failed to Get Version from GitVersion.");
            }

            return versionInfo;
        }
        catch (Exception ex)
        {
            // Check if the error is related to GitVersion failing due to no commits
            var errorMessage = ex.Message?.ToLower() ?? "";

            if (errorMessage.Contains("no commits found") || 
                errorMessage.Contains("process 'dotnet-gitversion") || 
                errorMessage.Contains("exited with code 1"))
            {
                Log.Warning("[WARN] GitVersion Failed (Likely no Commits Yet). Using Default Version 0.1.0-alpha.");
                
                // Return a default VersionInfo for repositories with no commits
                return new VersionInfo
                {
                    Version = "0.1.0-alpha",
                    FullVersion = "0.1.0-alpha.1+0",
                    Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    Hash = "0000000"
                };
            }
            
            // Re-throw if it's not a GitVersion no-commits issue
            throw;
        }
    }
}