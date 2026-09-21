#nullable enable

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Surface;

/// <summary>
/// Reads and writes the `surface-manifest.json` asset format from design/20-contract.md
/// (JSON, UTF-8 no BOM, LF, items sorted by (Kind, Name) ordinal).
/// </summary>
public static class SurfaceManifestSerializer
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    private sealed record ManifestDto(int ManifestSchemaVersion, string ProductVersion, List<ItemDto> Items);

    private sealed record ItemDto(string Kind, string Name, string Value, string? DeprecatedSince, string? RemoveIn);

    public static string Serialize(SurfaceManifest manifest)
    {
        var sortedItems = SortItems(manifest.Items)
            .Select(i => new ItemDto(i.Kind.ToString(), i.Name, i.Value, i.DeprecatedSince, i.RemoveIn))
            .ToList();

        var dto = new ManifestDto(manifest.ManifestSchemaVersion, manifest.ProductVersion, sortedItems);

        return JsonSerializer.Serialize(dto, Options);
    }

    public static void WriteToFile(SurfaceManifest manifest, string path)
    {
        File.WriteAllText(path, Serialize(manifest), Utf8NoBom);
    }

    public static SurfaceManifest Deserialize(string json)
    {
        ManifestDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, Options)
                ?? throw new SurfaceException(SurfaceErrorCode.BaselineUnreadable, "Manifest JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new SurfaceException(SurfaceErrorCode.BaselineUnreadable, $"Manifest JSON could not be parsed: {ex.Message}", ex);
        }

        SurfaceManifestValidator.EnsureSupportedSchemaVersion(dto.ManifestSchemaVersion);

        List<SurfaceItem> items;
        try
        {
            items = dto.Items
                .Select(i => new SurfaceItem(Enum.Parse<SurfaceItemKind>(i.Kind), i.Name, i.Value, i.DeprecatedSince, i.RemoveIn))
                .ToList();
        }
        catch (Exception ex) when (ex is not SurfaceException)
        {
            throw new SurfaceException(SurfaceErrorCode.BaselineUnreadable, $"Manifest item could not be parsed: {ex.Message}", ex);
        }

        SurfaceManifestValidator.EnsureNoDuplicateItems(items);

        return new SurfaceManifest(dto.ManifestSchemaVersion, dto.ProductVersion, items);
    }

    private static IEnumerable<SurfaceItem> SortItems(IReadOnlyList<SurfaceItem> items)
    {
        return items
            .OrderBy(i => i.Kind.ToString(), StringComparer.Ordinal)
            .ThenBy(i => i.Name, StringComparer.Ordinal);
    }
}
