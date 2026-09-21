#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;

namespace Surface;

/// <summary>
/// Compares a baseline manifest to a candidate manifest per design/20-contract.md's Surface
/// namespace semantics: (Kind, Name) identifies an item; a difference is compatible (never
/// blocking) only when it is ItemAdded or DeprecationAdded, or when the candidate's major
/// version is greater than the baseline's (SurfaceErrorCode.BlockingDifference: "outside the
/// compatible set with no major increase") — an intentional major bump can carry breaking
/// changes that would otherwise block.
/// </summary>
public static class SurfaceComparer
{
    private static readonly HashSet<SurfaceDifferenceKind> CompatibleKinds = new()
    {
        SurfaceDifferenceKind.ItemAdded,
        SurfaceDifferenceKind.DeprecationAdded,
    };

    public static SurfaceComparison Compare(SurfaceManifest baseline, SurfaceManifest candidate)
    {
        SurfaceManifestValidator.EnsureNoDuplicateItems(baseline.Items);
        SurfaceManifestValidator.EnsureNoDuplicateItems(candidate.Items);

        var baselineByKey = baseline.Items.ToDictionary(i => (i.Kind, i.Name));
        var candidateByKey = candidate.Items.ToDictionary(i => (i.Kind, i.Name));

        var all = new List<SurfaceDifference>();

        foreach (var key in candidateByKey.Keys.Except(baselineByKey.Keys))
        {
            var item = candidateByKey[key];
            all.Add(new SurfaceDifference(SurfaceDifferenceKind.ItemAdded, item.Kind, item.Name, null, item.Value));
        }

        foreach (var key in baselineByKey.Keys.Except(candidateByKey.Keys))
        {
            var item = baselineByKey[key];
            all.Add(new SurfaceDifference(SurfaceDifferenceKind.ItemRemoved, item.Kind, item.Name, item.Value, null));
        }

        foreach (var key in baselineByKey.Keys.Intersect(candidateByKey.Keys))
        {
            var b = baselineByKey[key];
            var c = candidateByKey[key];

            if (b.Value != c.Value)
            {
                all.Add(new SurfaceDifference(SurfaceDifferenceKind.ValueChanged, b.Kind, b.Name, b.Value, c.Value));
            }

            if (b.DeprecatedSince == null && c.DeprecatedSince != null)
            {
                all.Add(new SurfaceDifference(SurfaceDifferenceKind.DeprecationAdded, b.Kind, b.Name, b.DeprecatedSince, c.DeprecatedSince));
            }
            else if (b.DeprecatedSince != null && c.DeprecatedSince == null)
            {
                all.Add(new SurfaceDifference(SurfaceDifferenceKind.DeprecationRemoved, b.Kind, b.Name, b.DeprecatedSince, c.DeprecatedSince));
            }

            if (b.DeprecatedSince != null && c.DeprecatedSince != null && b.RemoveIn != c.RemoveIn)
            {
                all.Add(new SurfaceDifference(SurfaceDifferenceKind.RemovalTargetChanged, b.Kind, b.Name, b.RemoveIn, c.RemoveIn));
            }
        }

        var majorIncreased = HasMajorIncrease(baseline.ProductVersion, candidate.ProductVersion);
        var blocking = all.Where(d => !CompatibleKinds.Contains(d.Kind) && !majorIncreased).ToList();

        return new SurfaceComparison(all, blocking);
    }

    internal static bool HasMajorIncrease(string baselineVersion, string candidateVersion)
    {
        return TryGetMajor(candidateVersion, out var candidateMajor)
            && TryGetMajor(baselineVersion, out var baselineMajor)
            && candidateMajor > baselineMajor;
    }

    private static bool TryGetMajor(string version, out int major)
    {
        var segment = version.Split('.').FirstOrDefault() ?? string.Empty;
        return int.TryParse(segment, out major);
    }
}
