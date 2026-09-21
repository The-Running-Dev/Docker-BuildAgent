#nullable enable

using System.Collections.Generic;

namespace Surface.SurfaceDerivation;

/// <summary>
/// Derives one ConfigSchemaVersion item per supported configuration schema version.
/// The Config module (design/20-contract.md's `Config` namespace scaffold, slices S6/S7) does
/// not exist yet, so there is no owning declaration to read this from; this is a minimal,
/// provisional declaration (single version 1) that a future Config module should own and this
/// deriver should then read from instead.
/// </summary>
internal static class ConfigSchemaVersionDeriver
{
    private static readonly int[] SupportedConfigSchemaVersions = { 1 };

    public static List<SurfaceItem> Derive()
    {
        var items = new List<SurfaceItem>();

        foreach (var version in SupportedConfigSchemaVersions)
        {
            items.Add(new SurfaceItem(SurfaceItemKind.ConfigSchemaVersion, version.ToString(), "supported", null, null));
        }

        return items;
    }
}
