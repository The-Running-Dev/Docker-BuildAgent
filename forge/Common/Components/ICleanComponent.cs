using System;
using System.IO;
using Nuke.Common;

namespace Components;

/// <summary>
/// Build component for cleaning artifacts directory
/// </summary>
public interface ICleanComponent : INukeBuild
{
    /// <summary>
    /// Gets the artifacts directory path
    /// </summary>
    string ArtifactsDir { get; }

    /// <summary>
    /// Target for cleaning the artifacts directory
    /// </summary>
    Target Clean => _ => _
        .Executes(() =>
        {
            if (Directory.Exists(ArtifactsDir))
            {
                Directory.Delete(ArtifactsDir, true);
                Console.WriteLine("[OK] Cleaned Artifacts Directory");
            }

            Directory.CreateDirectory(ArtifactsDir);
        });
}