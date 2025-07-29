#nullable enable

using System.Collections.Generic;

using Newtonsoft.Json;

using Utilities;

namespace Entities;

/// <summary>
/// Configuration options for GitHubService release creation.
/// </summary>
public class GitHubReleaseOptions
{
    /// <summary>
    /// Gets or sets whether the release should be marked as a draft.
    /// </summary>
    public bool Draft { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the release should be marked as a prerelease.
    /// </summary>
    [JsonProperty("prerelease")]
    public bool PreRelease { get; set; } = false;

    /// <summary>
    /// Gets or sets the release name. If null, uses the tag name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether to include Docker images section in the release body.
    /// </summary>
    public bool IncludeDockerImages { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include changelog section in the release body.
    /// </summary>
    public bool IncludeChangeLog { get; set; } = true;

    /// <summary>
    /// Gets or sets custom sections to include in the release body.
    /// Key is the section title, Value is the section content.
    /// </summary>
    public Dictionary<string, string> CustomSections { get; set; } = new();

    /// <summary>
    /// Gets or sets the changelog formatting options.
    /// </summary>
    public ChangeLogFormatOptions? ChangeLogFormatOptions { get; set; }
}