#nullable enable
using Newtonsoft.Json;

namespace Entities;

/// <summary>
/// Represents version information for a software component, including semantic versioning, full version details, commit
/// date, and commit hash.
/// </summary>
/// <remarks>This record is typically used to encapsulate version-related metadata for a software release or
/// build.</remarks>
public record VersionInfo
{
    [JsonProperty("SemVer")]
    public string? Version { get; init; }

    [JsonProperty("FullVer")]
    public string? FullVersion { get; init; }

    [JsonProperty("CommitDate")]
    public string? Date { get; init; }

    [JsonProperty("Sha")]
    public string? Hash { get; init; }

    public override string? ToString() => Version;
}