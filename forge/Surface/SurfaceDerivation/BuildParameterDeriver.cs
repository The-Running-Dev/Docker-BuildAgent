#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Parameters;

namespace Surface.SurfaceDerivation;

/// <summary>
/// Derives one BuildParameter item per public property declared on each `*Params` class
/// (design/20-contract.md, "Build parameters": every public property of a *Params class is a
/// BuildParameter manifest item and a configuration key, I17). Reflects `DeclaredOnly` per
/// class rather than a merged/inherited view, so a property re-declared on a derived class
/// (ArtifactsDir on both NodeParams and NodeInDockerParams) is seen once per declaration and
/// deduplicated below when identical, per I17's key/parameter bijection.
/// </summary>
internal static class BuildParameterDeriver
{
    private static readonly Type[] ParamsTypes =
    {
        typeof(ForgeParams),
        typeof(DockerParams),
        typeof(NodeParams),
        typeof(NodeInDockerParams),
    };

    public static List<SurfaceItem> Derive()
    {
        var byName = new Dictionary<string, SurfaceItem>();

        foreach (var type in ParamsTypes)
        {
            var instance = Activator.CreateInstance(type)
                ?? throw new SurfaceException(SurfaceErrorCode.DerivationFailed, $"Could not construct a default instance of '{type.Name}'.");

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            foreach (var property in properties)
            {
                var defaultValue = property.GetValue(instance);
                var value = SurfaceValueFormatter.FormatParameterValue(property.PropertyType, defaultValue, type.Name, property.Name);
                var item = new SurfaceItem(SurfaceItemKind.BuildParameter, property.Name, value, null, null);

                if (byName.TryGetValue(property.Name, out var existing))
                {
                    if (existing.Value != item.Value)
                    {
                        throw new SurfaceException(
                            SurfaceErrorCode.DuplicateItem,
                            $"BuildParameter '{property.Name}' is declared more than once with different values ('{existing.Value}' vs '{item.Value}').");
                    }

                    continue;
                }

                byName[property.Name] = item;
            }
        }

        return byName.Values.ToList();
    }
}
