using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Entities;

namespace Utilities;

/// <summary>
/// Utility class for formatting change logs from commit data.
/// </summary>
public static class ChangeLogFormatter
{
    /// <summary>
    /// Formats a list of commits into a change log string with default formatting.
    /// </summary>
    /// <param name="commits">The list of commits to format.</param>
    /// <param name="config">The change log configuration.</param>
    /// <returns>A formatted change log string.</returns>
    public static string Format(List<CommitInfo> commits, ChangeLogConfig config)
    {
        return Format(commits, config, new ChangeLogFormatOptions());
    }

    /// <summary>
    /// Formats a list of commits into a change log string with custom formatting options.
    /// </summary>
    /// <param name="commits">The list of commits to format.</param>
    /// <param name="config">The change log configuration.</param>
    /// <param name="options">The formatting options.</param>
    /// <returns>A formatted change log string.</returns>
    public static string Format(List<CommitInfo> commits, ChangeLogConfig config, ChangeLogFormatOptions options)
    {
        if (commits.Count == 0)
        {
            return string.Empty;
        }

        // Group commits by date (descending: latest date first)
        var commitsByDate = commits
            .GroupBy(c => c.Date)
            .OrderByDescending(g => g.Key);

        var today = DateTime.UtcNow.ToString(options.DateFormat);
        var changelogBuilder = new StringBuilder();
        
        // Add header
        var header = config.Tag == null 
            ? $"## Complete History (Generated {today})"
            : $"## Changes Since {config.Tag} (Generated {today})";
        changelogBuilder.AppendLine($"{header}\n");

        // Add commits grouped by date
        foreach (var group in commitsByDate)
        {
            changelogBuilder.AppendLine($"### {group.Key}\n");

            foreach (var commit in group)
            {
                var line = options.IncludeHash 
                    ? $"- [{commit.Hash}] {commit.Message}"
                    : $"- {commit.Message}";
                    
                if (options.IncludeAuthor)
                {
                    line += $" ({commit.Author})";
                }
                
                changelogBuilder.AppendLine(line);
            }

            changelogBuilder.AppendLine();
        }

        return changelogBuilder.ToString();
    }
}

/// <summary>
/// Options for formatting change logs.
/// </summary>
public class ChangeLogFormatOptions
{
    /// <summary>
    /// Gets or sets the date format for the generated timestamp.
    /// </summary>
    public string DateFormat { get; set; } = "yyyy.MM.dd";

    /// <summary>
    /// Gets or sets whether to include commit hashes in the output.
    /// </summary>
    public bool IncludeHash { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include author names in the output.
    /// </summary>
    public bool IncludeAuthor { get; set; } = false;
}