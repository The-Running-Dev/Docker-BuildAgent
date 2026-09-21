#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Text;

namespace Surface;

/// <summary>
/// Formats a declared type and default value as the "{declared type}|{declared default}"
/// BuildParameter Value string required by design/20-contract.md's Surface namespace.
/// </summary>
internal static class SurfaceValueFormatter
{
    public static string FormatParameterValue(Type declaredType, object? declaredDefault, string ownerName, string propertyName)
    {
        return $"{FormatType(declaredType)}|{FormatDefault(declaredDefault, ownerName, propertyName)}";
    }

    public static string FormatType(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return $"{FormatType(underlying)}?";

        if (type.IsGenericType)
        {
            var name = type.Name.Substring(0, type.Name.IndexOf('`'));
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatType));
            return $"{name}<{args}>";
        }

        return type.Name;
    }

    private static string FormatDefault(object? value, string ownerName, string propertyName)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case bool b:
                return b ? "true" : "false";
            case string s:
                return s;
            case Enum e:
                return e.ToString();
            case IEnumerable enumerable:
                var items = new StringBuilder();
                var first = true;
                foreach (var item in enumerable)
                {
                    if (!first) items.Append(',');
                    items.Append(item);
                    first = false;
                }
                return items.ToString();
            case IFormattable formattable:
                return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
            default:
                throw new SurfaceException(
                    SurfaceErrorCode.DerivationFailed,
                    $"Cannot format default value of '{ownerName}.{propertyName}' (type {value.GetType()}) for the surface manifest.");
        }
    }
}
