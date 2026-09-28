#nullable enable

using System;
using System.IO;

using DocsCheck;
using Surface;

// Usage: DocsCheck [repository-root]
// Exit 0: no violation. Exit 1: at least one violation (the PR check fails). Exit 2: the surface could not be read.
var root = args.Length > 0 ? Path.GetFullPath(args[0]) : FindRoot(Directory.GetCurrentDirectory());

SurfaceManifest manifest;
try
{
    manifest = SurfaceDeriver.Derive(root, "0.0.0");
}
catch (SurfaceException ex)
{
    Console.Error.WriteLine($"The surface manifest could not be derived: {ex.Message}");
    return 2;
}

var report = DocsChecker.Check(root, manifest);
Console.WriteLine(report.Render());
return report.Success ? 0 : 1;

static string FindRoot(string start)
{
    var directory = new DirectoryInfo(start);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "forge", "Forge.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return start;
}
