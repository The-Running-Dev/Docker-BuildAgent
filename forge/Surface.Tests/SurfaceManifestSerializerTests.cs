using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Surface.Tests;

public class SurfaceManifestSerializerTests
{
    // S1.13: two items sharing (Kind, Name) in a manifest are rejected as DuplicateItem, at both
    // derivation-adjacent read time (here: deserializing a hand-authored manifest) and comparison time.
    [Fact]
    public void Deserialize_ManifestWithDuplicateItems_ThrowsDuplicateItem()
    {
        var json = """
        {
          "manifestSchemaVersion": 1,
          "productVersion": "2.0.0",
          "items": [
            { "kind": "BuildParameter", "name": "A", "value": "string|", "deprecatedSince": null, "removeIn": null },
            { "kind": "BuildParameter", "name": "A", "value": "string|other", "deprecatedSince": null, "removeIn": null }
          ]
        }
        """;

        var ex = Assert.Throws<SurfaceException>(() => SurfaceManifestSerializer.Deserialize(json));

        Assert.Equal(SurfaceErrorCode.DuplicateItem, ex.Code);
    }

    // S1.14: an unsupported/unrecognized manifestSchemaVersion is rejected outright, never silently
    // treated as an empty manifest.
    [Fact]
    public void Deserialize_UnsupportedSchemaVersion_ThrowsManifestSchemaUnsupported()
    {
        var json = """
        {
          "manifestSchemaVersion": 99,
          "productVersion": "2.0.0",
          "items": []
        }
        """;

        var ex = Assert.Throws<SurfaceException>(() => SurfaceManifestSerializer.Deserialize(json));

        Assert.Equal(SurfaceErrorCode.ManifestSchemaUnsupported, ex.Code);
    }

    [Fact]
    public void Deserialize_UnrecognizedItemKind_ThrowsBaselineUnreadable()
    {
        var json = """
        {
          "manifestSchemaVersion": 1,
          "productVersion": "2.0.0",
          "items": [
            { "kind": "NotAKind", "name": "A", "value": "string|", "deprecatedSince": null, "removeIn": null }
          ]
        }
        """;

        var ex = Assert.Throws<SurfaceException>(() => SurfaceManifestSerializer.Deserialize(json));

        Assert.Equal(SurfaceErrorCode.BaselineUnreadable, ex.Code);
    }

    [Fact]
    public void SerializeThenDeserialize_RoundTripsFieldsAndItemOrder()
    {
        var manifest = new SurfaceManifest(1, "2.0.0", new List<SurfaceItem>
        {
            new(SurfaceItemKind.BuildParameter, "Zeta", "string|z", null, null),
            new(SurfaceItemKind.BuildParameter, "Alpha", "string|a", "2.1.0", "3.0.0"),
            new(SurfaceItemKind.BuildType, "docker", "accepted", null, null),
        });

        var json = SurfaceManifestSerializer.Serialize(manifest);
        var roundTripped = SurfaceManifestSerializer.Deserialize(json);

        Assert.Equal(manifest.ManifestSchemaVersion, roundTripped.ManifestSchemaVersion);
        Assert.Equal(manifest.ProductVersion, roundTripped.ProductVersion);
        Assert.Equal(
            new[] { ("BuildParameter", "Alpha"), ("BuildParameter", "Zeta"), ("BuildType", "docker") },
            roundTripped.Items.Select(i => (i.Kind.ToString(), i.Name)));

        var alpha = roundTripped.Items.Single(i => i.Name == "Alpha");
        Assert.Equal("2.1.0", alpha.DeprecatedSince);
        Assert.Equal("3.0.0", alpha.RemoveIn);
    }

    [Fact]
    public void Serialize_ProducesNoBomWhenWrittenToFile()
    {
        var manifest = new SurfaceManifest(1, "2.0.0", new List<SurfaceItem>
        {
            new(SurfaceItemKind.BuildParameter, "A", "string|a", null, null),
        });

        var path = System.IO.Path.GetTempFileName();
        try
        {
            SurfaceManifestSerializer.WriteToFile(manifest, path);
            var bytes = System.IO.File.ReadAllBytes(path);

            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
