namespace Entities;

/// <summary>
/// Represents a mapping of a key to a value with an associated template.
/// </summary>
/// <remarks>This class is used to store a key-value pair along with a template string. It provides a convenient
/// way to manage and format mappings in a structured manner.</remarks>
/// <param name="key"></param>
/// <param name="template"></param>
/// <param name="value"></param>
public class MapFileValue(string key, string template, string value)
{
    /// <summary>
    /// Gets or sets the key associated with the current instance.
    /// </summary>
    public string Key { get; set; } = key;

    /// <summary>
    /// Gets or sets the string value associated with this instance.
    /// </summary>
    public string Value { get; set; } = value;

    /// <summary>
    /// Gets or sets the template string used for formatting output.
    /// </summary>
    public string Template { get; set; } = template;

    public override string ToString() => $"{Key}={Value}";
}