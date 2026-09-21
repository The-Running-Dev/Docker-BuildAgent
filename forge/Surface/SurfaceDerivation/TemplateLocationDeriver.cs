#nullable enable

using System.Collections.Generic;

namespace Surface.SurfaceDerivation;

/// <summary>
/// Derives one TemplateLocation item per Docker template discovery location, in the order
/// `forge/Docker/Docker.cs` and `forge/Common/Utilities/Docker.cs` actually try them (the two
/// files design/20-contract.md's "Docker template discovery" section names as "Declared at"):
/// (1) the explicit templates directory when it exists, (2) that same value resolved under the
/// root directory, (3) `&lt;templates&gt;/Dockerfile.&lt;appType&gt;` when no Dockerfile exists at the
/// configured path. Value is the zero-based position, per the contract's Value semantics for
/// TemplateLocation. This list is a direct transcription of that control flow, not something
/// read from source text, since the discovery order is control flow rather than a declared list.
/// </summary>
internal static class TemplateLocationDeriver
{
    private static readonly string[] Locations =
    {
        "ExplicitTemplatesDirectory",
        "RootRelativeTemplatesDirectory",
        "TemplateDockerfileByAppType",
    };

    public static List<SurfaceItem> Derive()
    {
        var items = new List<SurfaceItem>();

        for (var i = 0; i < Locations.Length; i++)
        {
            items.Add(new SurfaceItem(SurfaceItemKind.TemplateLocation, Locations[i], i.ToString(), null, null));
        }

        return items;
    }
}
