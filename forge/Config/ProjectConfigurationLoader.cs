using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Config;

/// <summary>
/// Discovers, parses and fully validates a project's `buildagent.yml`/`.yaml`/`.json`
/// (design/30-slices.md § S6). Reads only — never creates, modifies or migrates the file (S6.15,
/// S6.16). Every distinct error found is accumulated and reported together in one
/// <see cref="ConfigException"/> (I18, S6.13); only tiers 5 (<see
/// cref="ConfigurationTier.ProjectConfigurationFile"/>) and 6 (<see
/// cref="ConfigurationTier.DeclaredDefault"/>) are produced here — the remaining tiers are S7.
/// </summary>
public static class ProjectConfigurationLoader
{
    private static readonly string[] CandidateFileNames = { "buildagent.yml", "buildagent.yaml", "buildagent.json" };
    private static readonly long[] SupportedSchemaVersions = { 1 };

    /// <summary>
    /// Returns null when no project configuration file exists at <paramref name="projectRoot"/> —
    /// discovery finding nothing is not itself an error (S6.1). Throws <see cref="ConfigException"/>
    /// carrying every error found otherwise.
    /// </summary>
    public static ResolvedConfiguration? Load(string projectRoot)
    {
        var candidates = CandidateFileNames
            .Select(name => Path.Combine(projectRoot, name))
            .Where(File.Exists)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count > 1)
        {
            throw new ConfigException(new[]
            {
                new ConfigError(
                    ConfigErrorCode.MultipleConfigurationFiles,
                    null,
                    null,
                    $"More than one project configuration file was found: {string.Join(", ", candidates)}."),
            });
        }

        var path = candidates[0];
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException(new[]
            {
                new ConfigError(ConfigErrorCode.FileUnreadable, path, null, $"'{path}' exists but could not be opened: {ex.Message}"),
            });
        }

        var isYaml = !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        Dictionary<string, object?> document;
        try
        {
            document = ProjectConfigurationParser.Parse(text, isYaml);
        }
        catch (MalformedDocumentException ex)
        {
            throw new ConfigException(new[]
            {
                new ConfigError(ConfigErrorCode.MalformedDocument, path, null, $"'{path}' at line {ex.Line}, column {ex.Column}: {ex.Message}"),
            });
        }

        var errors = new List<ConfigError>();
        var resolved = new Dictionary<string, ResolvedValue>();

        ValidateSchemaVersion(document, path, errors);
        var paramsType = ValidateBuildType(document, path, errors, out var buildTypeKnown);

        if (buildTypeKnown)
        {
            ValidateParameters(document, path, paramsType, errors, resolved);
        }

        if (errors.Count > 0)
        {
            throw new ConfigException(errors);
        }

        return new ResolvedConfiguration(resolved);
    }

    private static void ValidateSchemaVersion(Dictionary<string, object?> document, string path, List<ConfigError> errors)
    {
        if (!document.TryGetValue("schemaVersion", out var raw) || raw is null)
        {
            errors.Add(new ConfigError(
                ConfigErrorCode.SchemaVersionMissing,
                path,
                "schemaVersion",
                $"'schemaVersion' is required. Supported values: {string.Join(", ", SupportedSchemaVersions)}."));
            return;
        }

        if (raw is not long version || !SupportedSchemaVersions.Contains(version))
        {
            errors.Add(new ConfigError(
                ConfigErrorCode.SchemaVersionUnsupported,
                path,
                "schemaVersion",
                $"'schemaVersion' must be one of: {string.Join(", ", SupportedSchemaVersions)}."));
        }
    }

    private static Type? ValidateBuildType(Dictionary<string, object?> document, string path, List<ConfigError> errors, out bool known)
    {
        document.TryGetValue("buildType", out var raw);

        if (raw is string buildType && BuildTypeCatalog.TryGetParamsType(buildType, out var paramsType))
        {
            known = true;
            return paramsType;
        }

        errors.Add(new ConfigError(
            ConfigErrorCode.UnknownBuildType,
            path,
            "buildType",
            $"'buildType' must be one of: {string.Join(", ", BuildTypeCatalog.AcceptedBuildTypes)}."));
        known = false;
        return null;
    }

    private static void ValidateParameters(
        Dictionary<string, object?> document,
        string path,
        Type? paramsType,
        List<ConfigError> errors,
        Dictionary<string, ResolvedValue> resolved)
    {
        var propertiesByKey = paramsType is null
            ? new Dictionary<string, PropertyInfo>()
            : paramsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p => KebabCase.FromPropertyName(p.Name), p => p, StringComparer.Ordinal);

        var parameters = document.TryGetValue("parameters", out var raw) && raw is Dictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>();

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, value) in parameters)
        {
            seen.Add(key);

            if (!propertiesByKey.TryGetValue(key, out var property))
            {
                var nearest = NearestKey.Find(key, propertiesByKey.Keys);
                var suffix = nearest is null ? "" : $" The nearest known key is '{nearest}'.";
                errors.Add(new ConfigError(ConfigErrorCode.UnknownKey, path, key, $"'{key}' is not a known parameter.{suffix}"));
                continue;
            }

            if (property.GetCustomAttribute<Parameters.SecretParameterAttribute>() is not null)
            {
                errors.Add(new ConfigError(
                    ConfigErrorCode.SecretKeyRejected,
                    path,
                    key,
                    $"'{key}' is secret-declared and may only be supplied by argument, environment variable or mapping file."));
                continue;
            }

            if (!ValueConversion.TryConvert(value, property.PropertyType, out var converted))
            {
                errors.Add(new ConfigError(
                    ConfigErrorCode.ValueTypeMismatch,
                    path,
                    key,
                    $"'{key}' expects a value of type '{property.PropertyType.Name}'; the file has a {ValueConversion.DescribeShape(value)}."));
                continue;
            }

            resolved[key] = new ResolvedValue(key, ValueConversion.Format(converted), ConfigurationTier.ProjectConfigurationFile, false);
        }

        if (paramsType is null)
        {
            return;
        }

        var defaultInstance = Activator.CreateInstance(paramsType)
            ?? throw new InvalidOperationException($"Could not construct a default instance of '{paramsType.Name}'.");

        foreach (var (key, property) in propertiesByKey)
        {
            if (seen.Contains(key))
            {
                continue;
            }

            var isSecret = property.GetCustomAttribute<Parameters.SecretParameterAttribute>() is not null;
            var defaultValue = property.GetValue(defaultInstance);

            if (defaultValue is null && !IsNullable(property))
            {
                errors.Add(new ConfigError(
                    ConfigErrorCode.RequiredValueMissing,
                    path,
                    key,
                    $"'{key}' has no value. Tiers consulted: project configuration file, declared default."));
                continue;
            }

            resolved[key] = new ResolvedValue(key, ValueConversion.Format(defaultValue), ConfigurationTier.DeclaredDefault, isSecret);
        }
    }

    private static bool IsNullable(PropertyInfo property)
    {
        if (property.PropertyType.IsValueType)
        {
            return Nullable.GetUnderlyingType(property.PropertyType) is not null;
        }

        var info = new NullabilityInfoContext().Create(property);
        return info.ReadState == NullabilityState.Nullable;
    }
}
