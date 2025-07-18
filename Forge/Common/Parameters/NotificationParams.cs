#nullable enable
using System;

namespace Parameters;

/// <summary>
/// Represents the parameters required for sending a build notification.
/// </summary>
/// <remarks>This class encapsulates information about a build process, including its outcome, duration, and
/// associated commit details. It is used to configure notifications sent to a specified webhook URL.</remarks>
public class NotificationParams
{
    public string? WebHookUrl { get; set; } = string.Empty;
    
    public bool BuildSucceeded { get; set; }
    
    public TimeSpan BuildDuration { get; set; }

    public string Commit { get; set; } = string.Empty;
    
    public string CommitMessage { get; set; } = string.Empty;
    
    public string Branch { get; set; } = string.Empty;
    
    public string? Version { get; set; }
    
    public (string RepoUrl, string BranchUrl, string CommitUrl) GitUrls { get; set; }
}