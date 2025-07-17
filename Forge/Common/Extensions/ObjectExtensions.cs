using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Extensions;

/// <summary>
/// Provides extension methods for object manipulation.
/// </summary>
/// <remarks>This class includes methods that extend the functionality of objects, allowing for operations such as
/// copying properties from one object to another.</remarks>
public static class ObjectExtensions
{
    private static readonly List<string> ExcludedProperties = ["Token", "Password", "Secret", "StripForDisplay"];

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
    public static string ToDisplayString(this object obj, int indent = 0)
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
            object value;

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
                sb.AppendLine($"{Indent(indent)}🔧 {displayName}: {value}");
            }
            else if (value is IEnumerable enumerable and not string)
            {
                sb.AppendLine($"{Indent(indent)}🔧 {displayName}: [");

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
                sb.AppendLine($"{Indent(indent)}🔧 {displayName}:");
                sb.Append(value.ToDisplayString(indent + 2));
            }
        }

        return sb.ToString();
    }
}