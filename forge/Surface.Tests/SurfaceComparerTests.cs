using System.Collections.Generic;
using Xunit;

namespace Surface.Tests;

public class SurfaceComparerTests
{
    private static SurfaceManifest Manifest(string productVersion, params SurfaceItem[] items)
    {
        return new SurfaceManifest(1, productVersion, items);
    }

    private static SurfaceItem Item(string name, string value, string? deprecatedSince = null, string? removeIn = null)
    {
        return new SurfaceItem(SurfaceItemKind.BuildParameter, name, value, deprecatedSince, removeIn);
    }

    // S1.5: only additions, deprecation-adds, and a new schema version produce zero blocking differences.
    [Fact]
    public void Compare_OnlyAdditionsAndDeprecationAdds_ProducesZeroBlockingDifferences()
    {
        var baseline = Manifest("2.0.0", Item("A", "string|"));
        var candidate = Manifest("2.0.0",
            Item("A", "string|", deprecatedSince: "2.1.0", removeIn: "3.0.0"),
            Item("B", "bool|false"));

        var comparison = SurfaceComparer.Compare(baseline, candidate);

        Assert.Empty(comparison.Blocking);
        Assert.Contains(comparison.All, d => d.Kind == SurfaceDifferenceKind.ItemAdded && d.Name == "B");
        Assert.Contains(comparison.All, d => d.Kind == SurfaceDifferenceKind.DeprecationAdded && d.Name == "A");
    }

    // S1.6: renaming a build parameter produces two differences (one removal, one addition); the removal is blocking.
    [Fact]
    public void Compare_RenamedParameter_ProducesOneRemovalAndOneAddition_RemovalIsBlocking()
    {
        var baseline = Manifest("2.0.0", Item("OldName", "string|"));
        var candidate = Manifest("2.0.0", Item("NewName", "string|"));

        var comparison = SurfaceComparer.Compare(baseline, candidate);

        Assert.Equal(2, comparison.All.Count);
        Assert.Contains(comparison.All, d => d.Kind == SurfaceDifferenceKind.ItemRemoved && d.Name == "OldName");
        Assert.Contains(comparison.All, d => d.Kind == SurfaceDifferenceKind.ItemAdded && d.Name == "NewName");
        Assert.Single(comparison.Blocking);
        Assert.Equal(SurfaceDifferenceKind.ItemRemoved, comparison.Blocking[0].Kind);
    }

    // S1.7: an item present in the baseline but absent from the candidate is a removal, regardless of cause.
    [Fact]
    public void Compare_ItemMissingFromCandidate_IsAlwaysItemRemoved()
    {
        var baseline = Manifest("2.0.0", Item("Gone", "string|"));
        var candidate = Manifest("2.0.0");

        var comparison = SurfaceComparer.Compare(baseline, candidate);

        var diff = Assert.Single(comparison.All);
        Assert.Equal(SurfaceDifferenceKind.ItemRemoved, diff.Kind);
        Assert.Equal("Gone", diff.Name);
    }

    // S1.8: changing an item's value blocks when the major version is unchanged; the failure names the
    // item, the baseline value, and the candidate value.
    [Fact]
    public void Compare_ValueChanged_MajorUnchanged_BlocksAndNamesBaselineAndCandidateValues()
    {
        var baseline = Manifest("2.0.0", Item("Port", "int|8080"));
        var candidate = Manifest("2.0.1", Item("Port", "int|9090"));

        var comparison = SurfaceComparer.Compare(baseline, candidate);

        var diff = Assert.Single(comparison.Blocking);
        Assert.Equal(SurfaceDifferenceKind.ValueChanged, diff.Kind);
        Assert.Equal("Port", diff.Name);
        Assert.Equal("int|8080", diff.BaselineValue);
        Assert.Equal("int|9090", diff.CandidateValue);
    }

    // S1.9: reordering Docker template discovery locations blocks when the major version is unchanged
    // (position is the Value for TemplateLocation, so reordering is a ValueChanged for an unchanged Name).
    [Fact]
    public void Compare_ReorderedTemplateLocations_MajorUnchanged_Blocks()
    {
        var baseline = Manifest("2.0.0", new SurfaceItem(SurfaceItemKind.TemplateLocation, "ExplicitTemplatesDirectory", "0", null, null));
        var candidate = Manifest("2.0.0", new SurfaceItem(SurfaceItemKind.TemplateLocation, "ExplicitTemplatesDirectory", "1", null, null));

        var comparison = SurfaceComparer.Compare(baseline, candidate);

        var diff = Assert.Single(comparison.Blocking);
        Assert.Equal(SurfaceDifferenceKind.ValueChanged, diff.Kind);
    }

    // S1.10: a difference kind outside the compatible set {ItemAdded, DeprecationAdded} blocks by default —
    // the comparer uses an allow-list, not a deny-list, so an unrecognized/uncommon kind still blocks.
    [Fact]
    public void Compare_DifferenceKindOutsideCompatibleSet_Blocks()
    {
        var baseline = Manifest("2.0.0", Item("A", "string|", deprecatedSince: "2.0.0", removeIn: "3.0.0"));
        var candidate = Manifest("2.0.0", Item("A", "string|", deprecatedSince: "2.0.0", removeIn: "4.0.0"));

        var comparison = SurfaceComparer.Compare(baseline, candidate);

        var diff = Assert.Single(comparison.Blocking);
        Assert.Equal(SurfaceDifferenceKind.RemovalTargetChanged, diff.Kind);
    }

    // A major version increase forgives an otherwise-blocking difference (SurfaceErrorCode.BlockingDifference:
    // "outside the compatible set with no major increase"). Not itself a numbered S1 criterion, but required
    // to prove the "(major unchanged)" qualifier in S1.8/S1.9 is real rather than incidental.
    [Fact]
    public void Compare_ValueChanged_MajorIncreased_DoesNotBlock()
    {
        var baseline = Manifest("2.0.0", Item("Port", "int|8080"));
        var candidate = Manifest("3.0.0", Item("Port", "int|9090"));

        var comparison = SurfaceComparer.Compare(baseline, candidate);

        Assert.Empty(comparison.Blocking);
        Assert.Single(comparison.All);
    }

    [Fact]
    public void Compare_DuplicateItemsInEitherManifest_ThrowsDuplicateItem()
    {
        var baseline = Manifest("2.0.0", Item("A", "string|"), Item("A", "string|other"));
        var candidate = Manifest("2.0.0", Item("A", "string|"));

        var ex = Assert.Throws<SurfaceException>(() => SurfaceComparer.Compare(baseline, candidate));

        Assert.Equal(SurfaceErrorCode.DuplicateItem, ex.Code);
    }
}
