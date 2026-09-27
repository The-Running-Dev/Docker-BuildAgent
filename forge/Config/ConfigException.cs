using System;
using System.Collections.Generic;
using System.Linq;

namespace Config;

/// <summary>
/// Carries every <see cref="ConfigError"/> found in one validation pass (I18, S6.13) — diverging
/// from Surface/Release's single-error-per-throw pattern deliberately, since Config's contract
/// requires reporting every distinct error in the file at once rather than failing on the first.
/// </summary>
public sealed class ConfigException : Exception
{
    public IReadOnlyList<ConfigError> Errors { get; }

    public ConfigException(IReadOnlyList<ConfigError> errors)
        : base(BuildMessage(errors))
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("A ConfigException must carry at least one error.", nameof(errors));
        }

        Errors = errors;
    }

    private static string BuildMessage(IReadOnlyList<ConfigError> errors)
    {
        var summary = string.Join("; ", errors.Select(e => $"{e.Code}: {e.Message}"));
        return $"Configuration is invalid ({errors.Count} error(s)): {summary}";
    }
}
