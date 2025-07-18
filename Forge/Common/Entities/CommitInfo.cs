namespace Entities;

/// <summary>
/// Represents information about a specific commit in a version control system.
/// </summary>
/// <remarks>This class provides details such as the commit hash, author, date, and message. It is typically used
/// to track changes in a repository and to display commit history.</remarks>
public class CommitInfo
{
    /// <summary>
    /// Gets or sets the unique identifier for the commit.
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Gets or sets the name or email of the person who made the commit.
    /// </summary>
    public string Author { get; set; }
    
    /// <summary>
    /// Gets or sets the date and time when the commit was made.
    /// </summary>
    public string Date { get; set; }
    
    /// <summary>
    /// Gets or sets the message describing the changes made in the commit.
    /// </summary>
    public string Message { get; set; }
}
