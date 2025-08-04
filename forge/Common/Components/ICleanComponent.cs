using System.IO;

using Extensions;

using Microsoft.Extensions.Logging;

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
    /// Gets the logger instance
    /// </summary>
    ILogger<NukeBuild> Logger { get; }

    /// <summary>
    /// Target for cleaning the artifacts directory
    /// </summary>
    Target Clean => _ => _
        .Executes(() =>
        {
            if (Directory.Exists(ArtifactsDir))
            {
                Directory.Delete(ArtifactsDir, true);
                
                Logger.Ok("Cleaned Artifacts Directory");
            }

            Directory.CreateDirectory(ArtifactsDir);
        });
}