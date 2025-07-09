using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;

public static class Git
{
    public static string GetLastTag()
    {
        var result = ProcessTasks.StartProcess("git", "describe --tags --abbrev=0", logOutput: false)
            .AssertZeroExitCode()
            .Output
            .Select(x => x.Text)
            .FirstOrDefault();

        return result?.Trim();
    }

    public static List<CommitInfo> GetCommitsSince(string tag = null)
    {
        var args = new List<string> { "log" };
        if (tag != null)
            args.Add($"{tag}..HEAD");
        args.AddRange(new[]
        {
            "--pretty=format:%h%x09%an%x09%ad%x09%s",
            "--date=short"
        });

        var output = ProcessTasks.StartProcess("git", args.Join(" "), logOutput: false)
            .AssertZeroExitCode()
            .Output
            .Select(x => x.Text)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var commits = output
            .Select(line =>
            {
                var parts = line.Split('\t');
                return new CommitInfo
                {
                    Hash = parts[0],
                    Author = parts[1],
                    Date = parts[2],
                    Message = parts[3]
                };
            })
            .ToList();

        return commits;
    }

    public static string GetChangelog()
    {
        var tag = GetLastTag();
        var commits = Git.GetCommitsSince(tag);

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

    public static (string repoUrl, string branchUrl, string commitUrl) GetRepoBranchCommitUrls(string branch, string commit)
    {
        string repoUrl = null;
        
        try
        {
            var remoteUrlOutput = ProcessTasks.StartProcess("git", "config --get remote.origin.url", logOutput: false)
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
}