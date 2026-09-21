#nullable enable

using System.Linq;
using System.Collections.Generic;

namespace Surface;

internal static class SurfaceManifestValidator
{
    public static readonly IReadOnlyCollection<int> SupportedManifestSchemaVersions = new[] { 1 };

    /// <summary>Throws DuplicateItem when two items in the manifest share (Kind, Name) (S1.13).</summary>
    public static void EnsureNoDuplicateItems(IReadOnlyList<SurfaceItem> items)
    {
        var seen = new HashSet<(SurfaceItemKind, string)>();

        foreach (var item in items)
        {
            var key = (item.Kind, item.Name);
            if (!seen.Add(key))
            {
                throw new SurfaceException(
                    SurfaceErrorCode.DuplicateItem,
                    $"Duplicate surface item: Kind='{item.Kind}', Name='{item.Name}'.");
            }
        }
    }

    /// <summary>Throws ManifestSchemaUnsupported for an unknown manifestSchemaVersion; never treats it as empty (I12, S1.14).</summary>
    public static void EnsureSupportedSchemaVersion(int manifestSchemaVersion)
    {
        if (!SupportedManifestSchemaVersions.Contains(manifestSchemaVersion))
        {
            throw new SurfaceException(
                SurfaceErrorCode.ManifestSchemaUnsupported,
                $"Manifest schema version {manifestSchemaVersion} is not supported by this reader.");
        }
    }
}
