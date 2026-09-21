using System;
using System.IO;

namespace Surface.Tests.TestSupport;

internal static class RepoRootLocator
{
    /// <summary>Walks up from the test binary's directory to the checkout root (marked by Forge.sln's parent).</summary>
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "forge", "Forge.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not locate the repository root above '{AppContext.BaseDirectory}'.");
    }
}
