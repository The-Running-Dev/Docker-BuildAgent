using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Utilities;

namespace Config;

/// <summary>
/// <see cref="BuildConfigurationOutcome"/> plus the tier-attributed resolution behind it.
/// </summary>
public sealed record ConfigurationGateOutcome(
    bool IsValid,
    IReadOnlyDictionary<string, string> ProjectFileValues,
    ResolvedConfiguration? Resolved) : BuildConfigurationOutcome(IsValid, ProjectFileValues);

/// <summary>
/// The Config-backed <see cref="IBuildConfigurationGate"/> a build type's entry point hands to
/// <c>Base.Build</c> (design/30-slices.md § S13). It turns the run's arguments, process environment
/// and mapping-file values into the keyed dictionaries <see cref="ConfigResolver"/> expects and
/// reports every <see cref="ConfigError"/> in one pass.
/// </summary>
public sealed class ProjectConfigurationGate : IBuildConfigurationGate
{
    /// <summary>
    /// Parameters the build derives itself when nothing supplies them (the repository URL comes from
    /// git), so an absent value is not a configuration error.
    /// </summary>
    private static readonly string[] DerivedKeys = { "repository-url" };

    private readonly string _buildType;

    public ProjectConfigurationGate(string buildType)
    {
        _buildType = buildType;
    }

    BuildConfigurationOutcome IBuildConfigurationGate.Resolve(
        string projectRoot,
        IReadOnlyList<string> invocationArguments,
        IReadOnlyDictionary<string, string> mappingFileEnvironment,
        Action<string> writeError) =>
        Resolve(projectRoot, invocationArguments, mappingFileEnvironment, writeError);

    public ConfigurationGateOutcome Resolve(
        string projectRoot,
        IReadOnlyList<string> invocationArguments,
        IReadOnlyDictionary<string, string> mappingFileEnvironment,
        Action<string> writeError)
    {
        BuildTypeCatalog.TryGetParamsType(_buildType, out var paramsType);
        var names = paramsType is null
            ? new Dictionary<string, string>()
            : paramsType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => KebabCase.FromPropertyName(p.Name), p => p.Name, StringComparer.Ordinal);

        var arguments = ParseArguments(invocationArguments, names.Keys);
        var process = new Dictionary<string, string?>(StringComparer.Ordinal);
        var mapping = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, name) in names)
        {
            var fromProcess = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(fromProcess))
            {
                process[key] = fromProcess;
            }

            if (mappingFileEnvironment.TryGetValue(name, out var fromMapping) && !string.IsNullOrEmpty(fromMapping))
            {
                mapping[key] = fromMapping;
            }
        }

        ResolvedConfiguration resolved;
        try
        {
            resolved = ConfigResolver.Resolve(_buildType, projectRoot, arguments, process, mapping);
        }
        catch (ConfigException first)
        {
            var errors = first.Errors
                .Where(e => !(e.Code == ConfigErrorCode.RequiredValueMissing && e.Key is not null && DerivedKeys.Contains(e.Key)))
                .ToList();

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    writeError($"Configuration error: file={error.File ?? "(none)"} key={error.Key ?? "(none)"} rule={error.Code}: {error.Message}");
                }

                return new ConfigurationGateOutcome(false, new Dictionary<string, string>(), null);
            }

            // Only derived keys were missing: resolve again with a placeholder so the file's values
            // (which cannot include the derived keys, or they would not have been missing) still resolve.
            foreach (var key in DerivedKeys.Where(k => names.ContainsKey(k) && !mapping.ContainsKey(k)))
            {
                mapping[key] = string.Empty;
            }

            resolved = ConfigResolver.Resolve(_buildType, projectRoot, arguments, process, mapping);
        }

        var fileValues = resolved.Values.Values
            .Where(v => v.Tier == ConfigurationTier.ProjectConfigurationFile && v.Value is not null)
            .ToDictionary(v => names[v.Key], v => v.Value!, StringComparer.Ordinal);

        return new ConfigurationGateOutcome(true, fileValues, resolved);
    }

    /// <summary>Reads <c>--key value</c> and <c>--key=value</c> for known parameter keys.</summary>
    private static Dictionary<string, string?> ParseArguments(IReadOnlyList<string> args, IEnumerable<string> knownKeys)
    {
        var known = new HashSet<string>(knownKeys, StringComparer.Ordinal);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var body = arg.Substring(2);
            var equals = body.IndexOf('=');
            if (equals >= 0)
            {
                var key = body.Substring(0, equals).ToLowerInvariant();
                if (known.Contains(key))
                {
                    result[key] = body.Substring(equals + 1);
                }
            }
            else if (known.Contains(body.ToLowerInvariant()) && i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result[body.ToLowerInvariant()] = args[i + 1];
                i++;
            }
        }

        return result;
    }
}
