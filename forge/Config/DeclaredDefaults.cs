using System;
using System.Collections.Generic;
using System.Reflection;

namespace Config;

/// <summary>
/// Computes tier-6 (<see cref="ConfigurationTier.DeclaredDefault"/>) values directly from a
/// `*Params` type's own defaults, independent of whether a project configuration file exists —
/// used by <see cref="ConfigResolver"/> when <see cref="ProjectConfigurationLoader.Load(string,bool)"/>
/// returns null (S7, no candidate file found).
/// </summary>
internal static class DeclaredDefaults
{
    public static Dictionary<string, ResolvedValue> Compute(Type paramsType)
    {
        var defaultInstance = Activator.CreateInstance(paramsType)
            ?? throw new InvalidOperationException($"Could not construct a default instance of '{paramsType.Name}'.");

        var result = new Dictionary<string, ResolvedValue>(StringComparer.Ordinal);

        foreach (var property in paramsType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = KebabCase.FromPropertyName(property.Name);
            var isSecret = property.GetCustomAttribute<Parameters.SecretParameterAttribute>() is not null;
            var defaultValue = property.GetValue(defaultInstance);

            if (defaultValue is null && !ProjectConfigurationLoader.IsNullable(property))
            {
                continue;
            }

            result[key] = new ResolvedValue(key, ValueConversion.Format(defaultValue), ConfigurationTier.DeclaredDefault, isSecret);
        }

        return result;
    }
}
