namespace Entities;

/// <summary>
/// Represents information about a specific commit in a version control system.
/// </summary>
/// <remarks>This class provides details such as the commit hash, author, date, and message. It is typically used
/// to track changes in a repository and to display commit history.</remarks>
public class CommitInfo
{
    public string Hash { get; set; }

    public string Author { get; set; }
    
    public string Date { get; set; }
    
    public string Message { get; set; }
}
