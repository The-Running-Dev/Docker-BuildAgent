using System;
using System.IO;

namespace Surface.Tests.TestSupport;

/// <summary>Copies just the real scripts/ files SurfaceDeriver reads into a scratch root, so
/// tests can create/delete extra files (like a stray surface-manifest.json) without touching
/// the actual working tree.</summary>
internal sealed class TempScriptRoot : IDisposable
{
    public string RootDirectory { get; }

    public TempScriptRoot()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), "SurfaceTests", Guid.NewGuid().ToString());
        var realRoot = RepoRootLocator.Find();

        CopyDirectory(Path.Combine(realRoot, "scripts", "nuke"), Path.Combine(RootDirectory, "scripts", "nuke"));
        CopyDirectory(Path.Combine(realRoot, "scripts", "powershell-module"), Path.Combine(RootDirectory, "scripts", "powershell-module"));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; a leftover temp dir does not affect test correctness.
        }
    }
}
