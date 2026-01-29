using System;

namespace Extensions;

/// <summary>
/// Extension methods for string sanitization and log-safe formatting.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Sanitizes a string for log output by flattening newlines and truncating length.
    /// </summary>
    /// <param name="value">The string to sanitize.</param>
    /// <param name="maxLength">Maximum length to keep before truncation.</param>
    /// <param name="emptyPlaceholder">Placeholder when the value is null or whitespace.</param>
    /// <returns>A log-safe string.</returns>
    public static string SanitizeForLog(this string value, int maxLength = 500, string emptyPlaceholder = "(no error body)")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return emptyPlaceholder;
        }

        var sanitized = value.Replace("\r", " ").Replace("\n", " ");

        if (maxLength > 0 && sanitized.Length > maxLength)
        {
            sanitized = sanitized[..maxLength] + "...";
        }

        return sanitized;
    }
}
