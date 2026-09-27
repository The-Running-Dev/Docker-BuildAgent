using System.Collections.Generic;
using System.Linq;

namespace Config;

/// <summary>
/// One resolved parameter value (design/20-contract.md, "Project configuration and precedence").
/// <see cref="Value"/> is null only when the parameter is declared nullable; a non-nullable
/// parameter with no value at any tier is a <see cref="ConfigErrorCode.RequiredValueMissing"/>,
/// never a null here.
/// </summary>
public sealed record ResolvedValue(
    string Key,
    string? Value,
    ConfigurationTier Tier,
    bool IsSecret);

/// <summary>
/// The full set of resolved values for one project (design/20-contract.md, "Project configuration
/// and precedence").
/// </summary>
public sealed record ResolvedConfiguration(
    IReadOnlyDictionary<string, ResolvedValue> Values)
{
    /// <summary>
    /// Secret values are redacted (I21) — never included even when the caller only intends to log
    /// or display this instance.
    /// </summary>
    public override string ToString()
    {
        var parts = Values.Values
            .OrderBy(v => v.Key, System.StringComparer.Ordinal)
            .Select(v => $"{v.Key}={(v.IsSecret ? "<redacted>" : v.Value ?? "<null>")} ({v.Tier})");

        return string.Join(", ", parts);
    }
}
