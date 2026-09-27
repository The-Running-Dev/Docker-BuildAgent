using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Config;

/// <summary>
/// Converts a parsed document value into a build parameter's declared CLR type, and describes a
/// value's shape without revealing the value itself (I25) for
/// <see cref="ConfigErrorCode.ValueTypeMismatch"/> messages.
/// </summary>
internal static class ValueConversion
{
    public static bool TryConvert(object? raw, Type declaredType, out object? converted)
    {
        if (declaredType == typeof(string))
        {
            if (raw is string s)
            {
                converted = s;
                return true;
            }
        }
        else if (declaredType == typeof(bool))
        {
            if (raw is bool b)
            {
                converted = b;
                return true;
            }
        }
        else if (declaredType.IsEnum)
        {
            if (raw is string enumText && Enum.TryParse(declaredType, enumText, ignoreCase: true, out var enumValue))
            {
                converted = enumValue;
                return true;
            }
        }
        else if (declaredType == typeof(int))
        {
            if (raw is long l && l >= int.MinValue && l <= int.MaxValue)
            {
                converted = (int)l;
                return true;
            }
        }
        else if (declaredType == typeof(long))
        {
            if (raw is long l)
            {
                converted = l;
                return true;
            }
        }
        else if (declaredType == typeof(double))
        {
            if (raw is double d)
            {
                converted = d;
                return true;
            }

            if (raw is long l)
            {
                converted = (double)l;
                return true;
            }
        }
        else if (declaredType == typeof(List<string>))
        {
            if (raw is List<object?> list && list.All(item => item is string))
            {
                converted = list.Cast<string>().ToList();
                return true;
            }
        }

        converted = null;
        return false;
    }

    public static string Format(object? value)
    {
        return value switch
        {
            null => "",
            bool b => b ? "true" : "false",
            List<string> list => string.Join(",", list),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    public static string DescribeShape(object? value)
    {
        return value switch
        {
            null => "null",
            string => "string",
            bool => "boolean",
            long => "number",
            double => "number",
            Dictionary<string, object?> => "object",
            List<object?> => "array",
            _ => "unknown",
        };
    }
}
