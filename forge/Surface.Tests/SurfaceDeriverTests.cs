using System.IO;
using System.Linq;
using Xunit;

using Surface.Tests.TestSupport;

namespace Surface.Tests;

public class SurfaceDeriverTests
{
    // S1.1: deriving twice from an unchanged tree produces byte-identical output.
    [Fact]
    public void Derive_CalledTwiceOnUnchangedTree_ProducesByteIdenticalManifests()
    {
        using var root = new TempScriptRoot();

        var first = SurfaceDeriver.Derive(root.RootDirectory, "2.0.0");
        var second = SurfaceDeriver.Derive(root.RootDirectory, "2.0.0");

        Assert.Equal(SurfaceManifestSerializer.Serialize(first), SurfaceManifestSerializer.Serialize(second));
    }

    // S1.2: one item per public build parameter, build type, Docker template discovery location,
    // exported PS module member, and supported schema version.
    [Fact]
    public void Derive_ProducesExactlyOneItemPerDeclaredSurfaceMember()
    {
        using var root = new TempScriptRoot();

        var manifest = SurfaceDeriver.Derive(root.RootDirectory, "2.0.0");

        Assert.Equal(22, manifest.Items.Count(i => i.Kind == SurfaceItemKind.BuildParameter));
        Assert.Equal(5, manifest.Items.Count(i => i.Kind == SurfaceItemKind.BuildType));
        Assert.Equal(3, manifest.Items.Count(i => i.Kind == SurfaceItemKind.TemplateLocation));
        Assert.Equal(3, manifest.Items.Count(i => i.Kind == SurfaceItemKind.ModuleCommand));
        Assert.Equal(9, manifest.Items.Count(i => i.Kind == SurfaceItemKind.ModuleParameter));
        Assert.Equal(1, manifest.Items.Count(i => i.Kind == SurfaceItemKind.ConfigSchemaVersion));

        var buildTypeNames = manifest.Items.Where(i => i.Kind == SurfaceItemKind.BuildType).Select(i => i.Name);
        Assert.Equal(new[] { "docker", "forge", "node", "node-in-docker", "node-template" }, buildTypeNames.OrderBy(n => n));

        var moduleCommandNames = manifest.Items.Where(i => i.Kind == SurfaceItemKind.ModuleCommand).Select(i => i.Name);
        Assert.Equal(new[] { "BuildAgentConfig", "Invoke-Build", "Set-BuildAgentConfig" }, moduleCommandNames.OrderBy(n => n));

        // The ArtifactsDir BuildParameter is declared independently on both NodeParams and
        // NodeInDockerParams with the same value; it dedupes to a single item (I17 bijection).
        Assert.Single(manifest.Items, i => i.Kind == SurfaceItemKind.BuildParameter && i.Name == "ArtifactsDir");
    }

    // S1.3: deleting all manifest files from the tree does not affect derivation output
    // (the deriver never reads a manifest from the working tree).
    [Fact]
    public void Derive_IsUnaffectedByPresenceOrAbsenceOfAManifestFileInTheTree()
    {
        using var root = new TempScriptRoot();
        var strayManifestPath = Path.Combine(root.RootDirectory, "surface-manifest.json");

        File.WriteAllText(strayManifestPath, "{\"manifestSchemaVersion\":1,\"productVersion\":\"9.9.9\",\"items\":[]}");
        var withManifestPresent = SurfaceDeriver.Derive(root.RootDirectory, "2.0.0");

        File.Delete(strayManifestPath);
        var withManifestAbsent = SurfaceDeriver.Derive(root.RootDirectory, "2.0.0");

        Assert.Equal(SurfaceManifestSerializer.Serialize(withManifestPresent), SurfaceManifestSerializer.Serialize(withManifestAbsent));
    }
}
