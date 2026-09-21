#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Surface.SurfaceDerivation;

/// <summary>
/// Minimal, deterministic parser for a PowerShell `param(...)` block. It parses only the
/// syntactic shapes the module and build script actually use (bracketed attributes, a bare
/// `[Type]$Name`, an optional `= default`), never executes PowerShell, and never uses a live
/// `pwsh`/`Import-Module` process so surface derivation stays byte-identical (S1.1).
/// </summary>
internal static class PowerShellParamBlockParser
{
    private static readonly Regex TypeAndName = new(
        @"\[(?<type>\w+)\]\s*\$(?<name>\w+)(?<rest>[\s\S]*)$",
        RegexOptions.Compiled);

    private static readonly Regex ValidateSetItem = new(
        @"'((?:[^'])*)'",
        RegexOptions.Compiled);

    public sealed record ParsedParameter(string Name, string Type, string? Default, IReadOnlyList<string>? ValidateSet);

    /// <summary>Extracts the content between the outermost balanced parens of the first `param(...)` block.</summary>
    public static string ExtractParamBlock(string source)
    {
        var match = Regex.Match(source, @"\bparam\s*\(", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, "No 'param(' block found.");
        }

        var start = match.Index + match.Length - 1; // index of the opening '('
        var depth = 0;
        var inSingleQuote = false;

        for (var i = start; i < source.Length; i++)
        {
            var c = source[i];

            if (inSingleQuote)
            {
                if (c == '\'') inSingleQuote = false;
                continue;
            }

            switch (c)
            {
                case '\'':
                    inSingleQuote = true;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(start + 1, i - start - 1);
                    }
                    break;
            }
        }

        throw new SurfaceException(SurfaceErrorCode.DerivationFailed, "Unbalanced 'param(' block.");
    }

    /// <summary>Splits a param block body into one chunk per declared parameter, at top-level commas only.</summary>
    public static List<string> SplitTopLevelParameters(string paramBlockBody)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var inSingleQuote = false;

        foreach (var c in paramBlockBody)
        {
            if (inSingleQuote)
            {
                current.Append(c);
                if (c == '\'') inSingleQuote = false;
                continue;
            }

            switch (c)
            {
                case '\'':
                    inSingleQuote = true;
                    current.Append(c);
                    break;
                case '(':
                case '[':
                case '{':
                    depth++;
                    current.Append(c);
                    break;
                case ')':
                case ']':
                case '}':
                    depth--;
                    current.Append(c);
                    break;
                case ',' when depth == 0:
                    chunks.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (current.Length > 0 && current.ToString().Trim().Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    /// <summary>Parses a single top-level parameter chunk into its name, declared type and default text.</summary>
    public static ParsedParameter ParseParameter(string chunk)
    {
        var match = TypeAndName.Match(chunk.Trim());
        if (!match.Success)
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"Could not parse parameter declaration: '{chunk.Trim()}'.");
        }

        var name = match.Groups["name"].Value;
        var type = match.Groups["type"].Value;
        var rest = match.Groups["rest"].Value.Trim();

        string? defaultText = null;
        if (rest.StartsWith("=", StringComparison.Ordinal))
        {
            defaultText = UnwrapStringLiteral(rest.Substring(1).Trim());
        }

        List<string>? validateSet = null;
        var validateSetMatch = Regex.Match(chunk, @"ValidateSet\(([\s\S]*?)\)");
        if (validateSetMatch.Success)
        {
            validateSet = new List<string>();
            foreach (Match itemMatch in ValidateSetItem.Matches(validateSetMatch.Groups[1].Value))
            {
                validateSet.Add(itemMatch.Groups[1].Value);
            }
        }

        return new ParsedParameter(name, type, defaultText, validateSet);
    }

    private static string UnwrapStringLiteral(string text)
    {
        if (text.Length >= 2 && text[0] == '\'' && text[^1] == '\'')
        {
            return text.Substring(1, text.Length - 2);
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            return text.Substring(1, text.Length - 2);
        }

        return text;
    }
}
