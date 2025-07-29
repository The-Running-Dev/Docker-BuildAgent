#nullable enable

namespace Entities;

/// <summary>
/// Represents a collection of URLs related to a GitService repository, including the repository URL, branch name, and commit
/// URL.
/// </summary>
public class GitUrls
{
    public string? Repository { get; set; }
    
    public string? Branch { get; set; }

    public string? Commit { get; set; }
}