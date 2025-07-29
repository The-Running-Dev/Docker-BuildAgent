#nullable enable
using System;

namespace Entities;

/// <summary>
/// Represents information about a GitHubService release.
/// </summary>
/// <remarks>
/// This class contains the essential information about a GitHubService release
/// that is commonly needed by applications working with GitHubService releases.
/// </remarks>
public class GitHubReleaseInfo
{
    /// <summary>
    /// Gets or sets the unique identifier of the release.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the tag name of the release.
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name/title of the release.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body/description of the release.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the release is a draft.
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the release is a prerelease.
    /// </summary>
    public bool Prerelease { get; set; }

    /// <summary>
    /// Gets or sets the creation date of the release.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the publication date of the release.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the URL of the release.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}