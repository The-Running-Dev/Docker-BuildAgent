using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Config;

/// <summary>
/// Parses a project configuration file's text into a plain document tree (`Dictionary&lt;string,
/// object?&gt;` / `List&lt;object?&gt;` / `string` / `bool` / `long` / `double` / `null`), so the
/// same validation logic runs regardless of whether the file was YAML or JSON — "the JSON form is
/// the same document" (design/20-contract.md, "Project configuration file"). Throws
/// <see cref="MalformedDocumentException"/> for anything not well-formed, or a YAML-only construct
/// with no JSON equivalent: an anchor, an alias, more than one document, or a non-string mapping
/// key (S6.11).
/// </summary>
internal static class ProjectConfigurationParser
{
    public static Dictionary<string, object?> Parse(string text, bool isYaml)
    {
        var root = isYaml ? ParseYaml(text) : ParseJson(text);

        if (root is not Dictionary<string, object?> document)
        {
            throw new MalformedDocumentException("The document's top level must be a mapping (an object).", 1, 1);
        }

        return document;
    }

    private static object? ParseYaml(string text)
    {
        RejectDisallowedConstructs(text);

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException ex)
        {
            throw new MalformedDocumentException(ex.Message, ex.Start.Line, ex.Start.Column);
        }

        if (stream.Documents.Count == 0)
        {
            throw new MalformedDocumentException("The file contains no YAML document.", 1, 1);
        }

        return ConvertYamlNode(stream.Documents[0].RootNode);
    }

    private static void RejectDisallowedConstructs(string text)
    {
        var parser = new Parser(new StringReader(text));
        var documentCount = 0;

        try
        {
            while (parser.MoveNext())
            {
                switch (parser.Current)
                {
                    case DocumentStart:
                        documentCount++;
                        break;
                    case AnchorAlias alias:
                        throw new MalformedDocumentException(
                            "A YAML alias has no JSON equivalent.", alias.Start.Line, alias.Start.Column);
                    case NodeEvent { Anchor.IsEmpty: false } anchored:
                        throw new MalformedDocumentException(
                            "A YAML anchor has no JSON equivalent.", anchored.Start.Line, anchored.Start.Column);
                }
            }
        }
        catch (YamlException ex)
        {
            throw new MalformedDocumentException(ex.Message, ex.Start.Line, ex.Start.Column);
        }

        if (documentCount > 1)
        {
            throw new MalformedDocumentException("The file contains more than one YAML document; only one is accepted.", 1, 1);
        }
    }

    private static object? ConvertYamlNode(YamlNode node)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                return ConvertYamlScalar(scalar);

            case YamlSequenceNode sequence:
                return sequence.Children.Select(ConvertYamlNode).ToList();

            case YamlMappingNode mapping:
                var dict = new Dictionary<string, object?>();
                foreach (var pair in mapping.Children)
                {
                    if (pair.Key is not YamlScalarNode keyScalar)
                    {
                        throw new MalformedDocumentException(
                            "A mapping key must be a plain string; this file has a non-string key.",
                            mapping.Start.Line, mapping.Start.Column);
                    }

                    dict[keyScalar.Value ?? string.Empty] = ConvertYamlNode(pair.Value);
                }

                return dict;

            default:
                throw new MalformedDocumentException("Unsupported YAML construct.", node.Start.Line, node.Start.Column);
        }
    }

    private static object? ConvertYamlScalar(YamlScalarNode scalar)
    {
        if (scalar.Style != YamlDotNet.Core.ScalarStyle.Plain)
        {
            return scalar.Value ?? string.Empty;
        }

        var text = scalar.Value;

        if (string.IsNullOrEmpty(text) || text is "~" || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (bool.TryParse(text, out var boolValue))
        {
            return boolValue;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        return text;
    }

    private static object? ParseJson(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            throw new MalformedDocumentException(ex.Message, (int)(ex.LineNumber ?? 0) + 1, (int)(ex.BytePositionInLine ?? 0) + 1);
        }

        using (document)
        {
            return ConvertJsonElement(document.RootElement);
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = ConvertJsonElement(property.Value);
                }

                return dict;

            case JsonValueKind.Array:
                return element.EnumerateArray().Select(ConvertJsonElement).ToList();

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                // Boxed explicitly: a bare `long : double` conditional promotes both arms to double.
                return element.TryGetInt64(out var longValue) ? (object)longValue : element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            default:
                throw new MalformedDocumentException("Unsupported JSON value.", 1, 1);
        }
    }
}
