using System;
using System.Collections.Generic;
using System.Reflection;

namespace Config;

/// <summary>
/// Merges every configuration tier into one <see cref="ResolvedConfiguration"/>
/// (design/30-slices.md § S7). A pure merge engine: tiers 1 (<see
/// cref="ConfigurationTier.InvocationArgument"/>), 3 (<see cref="ConfigurationTier.ProcessEnvironment"/>)
/// and 4 (<see cref="ConfigurationTier.MappingFileEnvironment"/>) are supplied by the caller as
/// already-keyed dictionaries (kebab-case parameter key to string value) — this class never parses
/// CLI arguments, reads process environment variables, or interprets mapping files itself. Tiers 5
/// and 6 come from <see cref="ProjectConfigurationLoader"/> and <see cref="DeclaredDefaults"/>.
/// </summary>
public static class ConfigResolver
{
    public static ResolvedConfiguration Resolve(
        string buildType,
        string projectRoot,
        IReadOnlyDictionary<string, string?> invocationArguments,
        IReadOnlyDictionary<string, string?> processEnvironment,
        IReadOnlyDictionary<string, string?> mappingFileEnvironment)
    {
        if (!BuildTypeCatalog.TryGetParamsType(buildType, out var paramsType))
        {
            throw new ConfigException(new[]
            {
                new ConfigError(
                    ConfigErrorCode.UnknownBuildType,
                    null,
                    "buildType",
                    $"'{buildType}' is not a known build type."),
            });
        }

        if (paramsType is null)
        {
            return new ResolvedConfiguration(new Dictionary<string, ResolvedValue>());
        }

        var lowerTiers = ProjectConfigurationLoader.Load(projectRoot, enforceRequired: false)?.Values
            ?? DeclaredDefaults.Compute(paramsType);

        var resolved = new Dictionary<string, ResolvedValue>(StringComparer.Ordinal);
        var errors = new List<ConfigError>();

        foreach (var property in paramsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = KebabCase.FromPropertyName(property.Name);
            var isSecret = property.GetCustomAttribute<Parameters.SecretParameterAttribute>() is not null;

            if (invocationArguments.TryGetValue(key, out var invocationValue))
            {
                resolved[key] = new ResolvedValue(key, invocationValue, ConfigurationTier.InvocationArgument, isSecret);
                continue;
            }

            if (processEnvironment.TryGetValue(key, out var processValue))
            {
                resolved[key] = new ResolvedValue(key, processValue, ConfigurationTier.ProcessEnvironment, isSecret);
                continue;
            }

            if (mappingFileEnvironment.TryGetValue(key, out var mappingValue))
            {
                resolved[key] = new ResolvedValue(key, mappingValue, ConfigurationTier.MappingFileEnvironment, isSecret);
                continue;
            }

            if (lowerTiers.TryGetValue(key, out var lowerValue))
            {
                resolved[key] = lowerValue with { IsSecret = isSecret };
                continue;
            }

            errors.Add(new ConfigError(
                ConfigErrorCode.RequiredValueMissing,
                null,
                key,
                $"'{key}' has no value. Tiers consulted: invocation argument, process environment, mapping file environment, project configuration file, declared default."));
        }

        if (errors.Count > 0)
        {
            throw new ConfigException(errors);
        }

        return new ResolvedConfiguration(resolved);
    }
}
