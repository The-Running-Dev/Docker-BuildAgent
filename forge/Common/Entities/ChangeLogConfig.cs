#nullable enable

namespace Entities;

/// <summary>
/// Represents the configuration for generating a change log, including the source and an optional tag.
/// </summary>
/// <remarks>This class is a pure data transfer object that contains only configuration data
/// without any business logic or service dependencies.</remarks>
public class ChangeLogConfig
{
    /// <summary>
    /// Gets or sets the source of the change log.
    /// </summary>
    public ChangeLogSource Source { get; set; }
    
    /// <summary>
    /// Gets or sets an optional tag associated with the object.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeLogConfig"/> class.
    /// </summary>
    public ChangeLogConfig()
    {
        Source = ChangeLogSource.LastTag;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeLogConfig"/> class with specified values.
    /// </summary>
    /// <param name="source">The source of the change log.</param>
    /// <param name="tag">The optional tag associated with the change log.</param>
    public ChangeLogConfig(ChangeLogSource source, string? tag = null)
    {
        Source = source;
        Tag = tag;
    }

    /// <summary>
    /// Creates a ChangeLogConfig instance from a string input for backward compatibility.
    /// </summary>
    /// <param name="input">A string representing the source of the change log. Can be null, empty, "all", or a specific tag.</param>
    /// <returns>A configured ChangeLogConfig instance.</returns>
    /// <remarks>
    /// This method is provided for backward compatibility and simple scenarios where dependency injection is not available.
    /// For more complex scenarios with proper dependency injection, use the IChangeLogConfigFactory service.
    /// Note: This method cannot retrieve the last tag automatically - it will create a LastTag config with null tag.
    /// </remarks>
    public static ChangeLogConfig FromString(string? input)
    {
        var trimmed = input?.Trim();

        return trimmed?.ToLowerInvariant() switch
        {
            null or "" => new ChangeLogConfig(ChangeLogSource.LastTag),
            "all" => new ChangeLogConfig(ChangeLogSource.All),
            _ => new ChangeLogConfig(ChangeLogSource.SpecificTag, trimmed)
        };
    }
}