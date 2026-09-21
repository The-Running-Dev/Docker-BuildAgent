#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;

using Surface.SurfaceDerivation;

namespace Surface;

/// <summary>
/// Derives the candidate SurfaceManifest by mechanically reading the product's declared public
/// surface from the working tree (I47: derived, never authored; I3/I8-adjacent: never reads a
/// manifest file from the tree, only the declarations that define the surface). Only the item
/// kinds S1's acceptance criteria require are covered: BuildParameter, BuildType,
/// TemplateLocation, ModuleCommand/ModuleParameter, ConfigSchemaVersion. The remaining
/// SurfaceItemKind values (ToolCommand, ToolParameter, ImageInvocation, ImageMountPoint,
/// ImageEnvironmentInput) belong to the still-greenfield Global tool and Image surfaces and are
/// left to the slice that defines them.
/// </summary>
public static class SurfaceDeriver
{
    public const int CurrentManifestSchemaVersion = 1;

    public static SurfaceManifest Derive(string rootDirectory, string productVersion)
    {
        List<SurfaceItem> items;
        try
        {
            items = new List<SurfaceItem>()
                .Concat(BuildParameterDeriver.Derive())
                .Concat(BuildTypeDeriver.Derive(rootDirectory))
                .Concat(TemplateLocationDeriver.Derive())
                .Concat(ModuleSurfaceDeriver.Derive(rootDirectory))
                .Concat(ConfigSchemaVersionDeriver.Derive())
                .ToList();
        }
        catch (Exception ex) when (ex is not SurfaceException)
        {
            throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"Surface derivation failed: {ex.Message}", ex);
        }

        SurfaceManifestValidator.EnsureNoDuplicateItems(items);

        var sorted = items
            .OrderBy(i => i.Kind.ToString(), StringComparer.Ordinal)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();

        return new SurfaceManifest(CurrentManifestSchemaVersion, productVersion, sorted);
    }
}
