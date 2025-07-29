#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Nuke.Common;

namespace Extensions;

/// <summary>
/// Provides extension methods for object manipulation.
/// </summary>
/// <remarks>This class includes methods that extend the functionality of objects, allowing for operations such as
/// copying properties from one object to another.</remarks>
public static class ObjectExtensions
{
    public enum Source { CommandLine, Environment, Default }

    private static readonly List<string> ExcludedProperties = ["RegistryToken", "Password", "Secret", "StripForDisplay"];

    private static string Indent(int level) => new(' ', level * 2);

    /// <summary>
    /// Creates a new instance of the specified target type and copies the values of matching properties from the source
    /// object.
    /// </summary>
    /// <remarks>Only public instance properties with matching names and types that are writable in the target
    /// type will be copied.</remarks>
    /// <typeparam name="TTarget">The type of the target object to which properties will be copied.</typeparam>
    /// <param name="source">The source object from which property values are copied. Cannot be <see langword="null"/>.</param>
    /// <returns>A new instance of <typeparamref name="TTarget"/> with property values copied from the source object.</returns>
    public static TTarget CopyTo<TTarget>(this object source)
    {
        var target = Activator.CreateInstance<TTarget>();
        var sourceProps = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var targetProps = typeof(TTarget).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var sourceProp in sourceProps)
        {
            var targetProp = targetProps.FirstOrDefault(x =>
                x.Name == sourceProp.Name &&
                x.PropertyType == sourceProp.PropertyType &&
                x.CanWrite);

            if (targetProp != null)
            {
                var value = sourceProp.GetValue(source);

                targetProp.SetValue(target, value);
            }
        }

        return target;
    }

    //public static void Hydrate<TBuild>(this object target, TBuild build, bool verbose = false)
    //{
    //    var buildType = typeof(TBuild);
    //    var paramType = target.GetType();

    //    var buildFields = buildType
    //        .GetFields(BindingFlags.Public | BindingFlags.Instance)
    //        .Where(f => Attribute.IsDefined(f, typeof(ParameterAttribute)))
    //        .ToDictionary(f => f.Name, f => f);

    //    foreach (var paramProp in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    //    {
    //        if (!paramProp.CanWrite || !buildFields.TryGetValue(paramProp.Name, out var sourceField))
    //            continue;

    //        var cliValue = sourceField.GetValue(build);
    //        var currentValue = paramProp.GetValue(target);
    //        var defaultValue = GetDefaultValue(paramProp.PropertyType);

    //        var isUnset =
    //            currentValue == null ||
    //            (paramProp.PropertyType == typeof(string) && string.IsNullOrWhiteSpace(currentValue as string)) ||
    //            Equals(currentValue, defaultValue);

    //        if (!Equals(cliValue, null) && !Equals(cliValue, defaultValue))
    //        {
    //            paramProp.SetValue(target, cliValue);

    //            if (verbose)
    //            {
    //                LogSource(paramProp.Name, cliValue, Source.CommandLine);
    //            }
    //        }
    //        else if (isUnset && !Equals(currentValue, cliValue))
    //        {
    //            paramProp.SetValue(target, cliValue);

    //            if (verbose)
    //            {
    //                LogSource(paramProp.Name, cliValue, Source.Environment);
    //            }
    //        }
    //        else if (verbose)
    //        {
    //            LogSource(paramProp.Name, currentValue, Source.Default);
    //        }
    //    }
    //}

    //static object? GetDefaultValue(Type t) =>
    //        t.IsValueType ? Activator.CreateInstance(t) : null;

    //static void LogSource(string name, object? value, Source source)
    //{
    //    Console.WriteLine($"[Param] {name} = {value ?? "null"} (from {source})");
    //}

    /// <summary>
    /// Converts an object to a formatted string representation, displaying its public properties and their values.
    /// </summary>
    /// <remarks>Properties that are indexers or match certain exclusion patterns are omitted from the output.
    /// If the object has a property named "StripForDisplay", its value is used as a set of patterns to strip from
    /// string properties.</remarks>
    /// <param name="obj">The object to be converted to a display string. If <see langword="null"/>, the method returns "null".</param>
    /// <param name="indent">The number of spaces to use for indentation in the output string. Defaults to 0.</param>
    /// <returns>A string representation of the object, with each property displayed on a new line, indented according to the
    /// specified level. Complex objects and collections are recursively expanded.</returns>
    public static string ToDisplayString(this object? obj, int indent = 0)
    {
        if (obj == null)
        {
            return $"{Indent(indent)}null";
        }

        var type = obj.GetType();
        var sb = new StringBuilder();
        var properties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(p => p.Name);

        // Look for StripForDisplay property
        var stripPatterns = type
            .GetProperty("StripForDisplay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(obj) as string[];

        foreach (var prop in properties)
        {
            object? value;

            if (ExcludedProperties.Any(x => prop.Name.Contains(x, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Skip indexers
            if (prop.GetIndexParameters().Length > 0)
            {
                continue;
            }

            value = prop.GetValue(obj, null);
            var displayName = prop.Name;

            // Strip webhook domain
            if (prop.Name.ToLower().Contains("webhook") && value is string strVal)
            {
                value = Regex.Replace(strVal, @"^(https?://[^/]+).*", "$1...");
            }

            // Apply strip patterns
            if (value is string valStr && stripPatterns != null)
            {
                foreach (var pattern in stripPatterns)
                {
                    valStr = Regex.Replace(valStr, pattern, "", RegexOptions.IgnoreCase);
                }

                value = valStr;
            }

            // Simple types
            if (value is null || value is string || value.GetType().IsPrimitive)
            {
                sb.AppendLine($"{Indent(indent)}[CONFIG] {displayName}: {value}");
            }
            else if (value.GetType().IsEnum)
            {
                sb.AppendLine($"{Indent(indent)}[CONFIG] {displayName}: {value}");
            }
            else if (value is IEnumerable enumerable and not string)
            {
                sb.AppendLine($"{Indent(indent)}[CONFIG] {displayName}: [");

                foreach (var item in enumerable)
                {
                    if (item is null)
                    {
                        sb.AppendLine($"{Indent(indent + 2)}- null");
                    }
                    else if (item is string or ValueType) // ✅ handle simple types
                    {
                        sb.AppendLine($"{Indent(indent + 2)}- {item}");
                    }
                    else
                    {
                        sb.AppendLine(item.ToDisplayString(indent + 2));
                    }
                }

                sb.AppendLine($"{Indent(indent)}]");
            }
            // Nested object
            else
            {
                sb.AppendLine($"{Indent(indent)}[CONFIG] {displayName}:");
                sb.Append(value.ToDisplayString(indent + 2));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Populates the properties of the target object with values from the specified build object.
    /// </summary>
    /// <remarks>This method sets the properties of the <paramref name="target"/> object to the values of the
    /// corresponding fields in the <paramref name="build"/> object, provided the fields are marked with the <see
    /// cref="ParameterAttribute"/>. If a property on the target is already set to a non-default value, it will not be
    /// overwritten unless the corresponding field in the build object is also non-default.</remarks>
    /// <typeparam name="TBuild">The type of the build object containing the source values.</typeparam>
    /// <param name="target">The object whose properties are to be set.</param>
    /// <param name="build">The object containing the source values for the properties.</param>
    /// <param name="verbose">A boolean value indicating whether to log the source of each property value. <see langword="true"/> to enable
    /// logging; otherwise, <see langword="false"/>.</param>
    public static void Hydrate<TBuild>(this object target, TBuild build, bool verbose = false)
    {
        var buildType = typeof(TBuild);
        var paramType = target.GetType();

        var buildFields = buildType
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => Attribute.IsDefined(f, typeof(ParameterAttribute)))
            .ToDictionary(f => f.Name, f => f);

        foreach (var paramProp in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!paramProp.CanWrite || !buildFields.TryGetValue(paramProp.Name, out var sourceField))
                continue;

            var cliValue = sourceField.GetValue(build);
            var currentValue = paramProp.GetValue(target);
            var defaultValue = GetDefaultValue(paramProp.PropertyType);

            var isUnset =
                currentValue == null ||
                (paramProp.PropertyType == typeof(string) && string.IsNullOrWhiteSpace(currentValue as string)) ||
                Equals(currentValue, defaultValue);

            if (!Equals(cliValue, null) && !Equals(cliValue, defaultValue))
            {
                paramProp.SetValue(target, cliValue);
                if (verbose)
                    LogSource(paramProp.Name, cliValue, Source.CommandLine); // Technically "Nuke-bound env"
            }
            else if (verbose)
            {
                LogSource(paramProp.Name, currentValue, Source.Default);
            }
        }

        static object? GetDefaultValue(Type t) =>
            t.IsValueType ? Activator.CreateInstance(t) : null;

        static void LogSource(string name, object? value, Source source)
        {
            Console.WriteLine($"[Parameter] {name} = {value ?? "null"} ({source})");
        }
    }
}