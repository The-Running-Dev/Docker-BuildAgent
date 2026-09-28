using Surface;

namespace DocsCheck.Tests.TestSupport;

/// <summary>A throwaway repository root that tests populate with documents and tree files.</summary>
internal sealed class TempRepo : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "docscheck-" + Guid.NewGuid().ToString("N"));

    public TempRepo() => Directory.CreateDirectory(Root);

    public TempRepo Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return this;
    }

    public static SurfaceManifest Manifest(params SurfaceItem[] items) => new(1, "2.0.0", items);

    public static SurfaceItem Item(SurfaceItemKind kind, string name, string? deprecatedSince = null) =>
        new(kind, name, "v", deprecatedSince, deprecatedSince == null ? null : "3.0.0");

    public static SurfaceManifest StandardManifest() => Manifest(
        Item(SurfaceItemKind.BuildType, "docker"),
        Item(SurfaceItemKind.BuildType, "node"),
        Item(SurfaceItemKind.BuildParameter, "ImageTag"),
        Item(SurfaceItemKind.BuildParameter, "ArtifactsDir"),
        Item(SurfaceItemKind.ModuleCommand, "Invoke-Build"),
        Item(SurfaceItemKind.ModuleCommand, "Set-BuildAgentConfig"),
        Item(SurfaceItemKind.ModuleParameter, "Invoke-Build.type"),
        Item(SurfaceItemKind.ModuleParameter, "Invoke-Build.args"),
        Item(SurfaceItemKind.TemplateLocation, "ExplicitTemplatesDirectory"));

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }
}
