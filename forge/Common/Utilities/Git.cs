using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using Serilog;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Utilities;


using Entities;

namespace Utilities;

/// <summary>
/// Provides utility methods for interacting with a Git repository.
/// </summary>
/// <remarks>This static class includes methods for retrieving commit messages, generating changelogs, managing
/// tags, and constructing repository URLs. It is designed to facilitate common Git operations
/// programmatically.</remarks>
public static class Git
{
    /// <summary>
    /// Gets the commit message of the most recent Git commit.
    /// </summary>
    /// <remarks>This property retrieves the commit message by executing a Git command to fetch the latest
    /// commit's message. The message is trimmed of whitespace and limited to the first 20 non-empty lines.</remarks>
    public static string CommitMessage
    {
        get {
            var commitMessage = ProcessTasks
                .StartProcess("git", "log -1 --pretty=%B", logOutput: false, logInvocation: false)
                .AssertZeroExitCode()
                .Output
                .Select(o => o.Text.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(20)
                .ToList();

            return string.Join("\n", commitMessage);
        }
    }
   
    /// <summary>
    /// Gets the changelog as a formatted string, listing all commits since the last tag.
    /// </summary>
    /// <remarks>The changelog is grouped by date in descending order, with the most recent date first. Each
    /// group contains the commit messages for that date.</remarks>
    public static string ChangeLog
    {
        get
        {
            var tag = GetLastTag();
            var commits = GetCommitsSince(tag);

            if (commits.Count == 0)
            {
                return string.Empty;
            }

            // Group commits by date (descending: latest date first)
            var commitsByDate = commits
                .GroupBy(c => c.Date)
                .OrderByDescending(g => g.Key);

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var changelogBuilder = new StringBuilder();
            changelogBuilder.AppendLine($"## {today}\n");

            foreach (var group in commitsByDate)
            {
                changelogBuilder.AppendLine($"### {group.Key}\n");

                foreach (var commit in group)
                {
                    changelogBuilder.AppendLine($"- {commit.Message}");
                }

                changelogBuilder.AppendLine();
            }

            return changelogBuilder.ToString();
        }
    }

    /// <summary>
    /// Retrieves the most recent Git tag from the current repository.
    /// </summary>
    /// <remarks>This method executes a Git command to obtain the latest tag and trims any surrounding
    /// whitespace from the result.</remarks>
    /// <returns>A string representing the latest Git tag, or <see langword="null"/> if no tags are found.</returns>
    public static string GetLastTag()
    {
        var process = ProcessTasks.StartProcess("git", "describe --tags --abbrev=0", logOutput: false, logInvocation: false);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return null;
        }

        var result = process.Output
            .Select(x => x.Text)
            .FirstOrDefault();

        return result?.Trim();
    }

    /// <summary>
    /// Retrieves a list of commits from the current Git repository since a specified tag.
    /// </summary>
    /// <remarks>The method uses the Git command line tool to fetch commit information, including the commit
    /// hash, author, date, and message.</remarks>
    /// <param name="tag">The Git tag from which to start listing commits. If <see langword="null"/>, all commits are retrieved.</param>
    /// <returns>A list of <see cref="CommitInfo"/> objects representing the commits since the specified tag. If no tag is
    /// specified, returns all commits.</returns>
    public static List<CommitInfo> GetCommitsSince(string tag = null)
    {
        var args = new List<string> { "log" };

        if (tag != null)
        {
            args.Add($"{tag}..HEAD");
        }

        args.AddRange(new[]
        {
            "--pretty=format:%h%x09%an%x09%ad%x09%s",
            "--date=short"
        });

        var output = ProcessTasks.StartProcess("git", args.Join(" "), logOutput: false, logInvocation: false)
            .AssertZeroExitCode()
            .Output
            .Select(x => x.Text)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var commits = output
            .Select(line =>
            {
                var parts = line.Split('\t', 4);
                if (parts.Length < 4)
                {
                    return null;
                }

                return new CommitInfo
                {
                    Hash = parts[0],
                    Author = parts[1],
                    Date = parts[2],
                    Message = parts[3]
                };
            })
            .Where(x => x != null)
            .ToList();

        return commits;
    }

    /// <summary>
    /// Generates URLs for the repository, branch, and commit based on the current Git configuration.
    /// </summary>
    /// <remarks>This method attempts to retrieve the remote origin URL from the Git configuration and convert
    /// it  into a web URL format. If the branch or commit is specified as "unknown", the corresponding URL  will be
    /// <see langword="null"/>.</remarks>
    /// <param name="branch">The name of the branch for which to generate the URL. Must not be "unknown".</param>
    /// <param name="commit">The commit hash for which to generate the URL. Must not be "unknown".</param>
    /// <returns>A tuple containing the repository URL, branch URL, and commit URL.  If the repository URL cannot be determined,
    /// the branch and commit URLs will be <see langword="null"/>.</returns>
    public static (string repoUrl, string branchUrl, string commitUrl) Urls(string branch, string commit)
    {
        string repoUrl = null;
        
        try
        {
            var remoteUrlOutput = ProcessTasks.StartProcess("git", "config --get remote.origin.url", logOutput: false, logInvocation: false)
                .AssertZeroExitCode()
                .Output;
            var remoteUrl = remoteUrlOutput.Select(x => x.Text?.Trim()).FirstOrDefault(x => !string.IsNullOrEmpty(x));

            if (!string.IsNullOrEmpty(remoteUrl))
            {
                // Convert SSH or HTTPS to web URL
                if (remoteUrl.StartsWith("git@"))
                {
                    // git@github.com:owner/repo.git => https://github.com/owner/repo
                    repoUrl = "https://" + remoteUrl.Substring(4).Replace(":", "/").Replace(".git", "");
                }
                else if (remoteUrl.StartsWith("https://"))
                {
                    repoUrl = remoteUrl.Replace(".git", "");
                }
            }
        }
        catch { /* ignore */ }
        
        var branchUrl = !string.IsNullOrEmpty(repoUrl) && branch != "unknown" ? $"{repoUrl}/tree/{branch}" : null;
        var commitUrl = !string.IsNullOrEmpty(repoUrl) && commit != "unknown" ? $"{repoUrl}/commit/{commit}" : null;
        
        return (repoUrl, branchUrl, commitUrl);
    }

    /// <summary>
    /// Creates a new Git tag and pushes it to the remote repository if it does not already exist.
    /// </summary>
    /// <remarks>If the specified tag already exists, no new tag is created, and a log entry is made
    /// indicating the tag's existence. If the tag does not exist, it is created and pushed to the remote repository,
    /// overwriting any existing tag with the same name.</remarks>
    /// <param name="tag">The name of the tag to create and push. Cannot be null or empty.</param>
    public static void CreateTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag cannot be null or empty.", nameof(tag));
        }

        if (!IsValidTag(tag))
        {
            throw new ArgumentException($"Invalid tag format: {tag}", nameof(tag));
        }

        var existingTags = GitTasks.Git("tag");

        if (existingTags.All(l => !string.Equals(l.Text?.Trim(), tag, StringComparison.OrdinalIgnoreCase)))
        {
            GitTasks.Git($"tag -f \"{tag}\"");
            GitTasks.Git($"push origin -f \"{tag}\"");

            Log.Information($"🏷️ Created and Pushed Tag: {tag}");
        }
        else
        {
            Log.Information($"✅ Tag {tag} Already Exists");
        }
    }

    private static bool IsValidTag(string tag)
    {
        return Regex.IsMatch(tag, "^[A-Za-z0-9][A-Za-z0-9._-]*$");
    }

    /// <summary>
    /// Marks the specified directory as a safe directory for Git operations.
    /// </summary>
    /// <remarks>This method configures Git to recognize the specified directory as safe, allowing operations
    /// to be performed without warnings about potential security risks. It uses the Git command line to add the
    /// directory to the global safe directory list.</remarks>
    /// <param name="directoryPath">The path of the directory to be marked as safe. Cannot be null or empty.</param>
    public static void SetSafeDirectory(string directoryPath)
    {
        ProcessTasks.StartProcess("git", $"config --global --add safe.directory \"{directoryPath}\"", logOutput: false, logInvocation: false).AssertZeroExitCode();
    }
}