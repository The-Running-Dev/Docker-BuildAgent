using System.IO;

namespace Extensions;

/// <summary>
/// Provides extension methods for performing input/output operations on directories.
/// </summary>
public static class IoExtensions
{
    /// <summary>
    /// Copies all files and subdirectories from the specified source directory to the target directory.
    /// </summary>
    /// <remarks>This method recursively copies all files and subdirectories from the source directory to the
    /// target directory. Existing files in the target directory with the same name will be overwritten.</remarks>
    /// <param name="sourceDir">The path of the directory to copy from. Must be a valid directory path.</param>
    /// <param name="targetDir">The path of the directory to copy to. If the directory does not exist, it will be created.</param>
    public static void CopyDirectory(this string sourceDir, string targetDir)
    {
        // Create all subdirectories in advance
        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var newDir = dir.Replace(sourceDir, targetDir);

            Directory.CreateDirectory(newDir);
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var destFile = file.Replace(sourceDir, targetDir);
        
            // Ensure the destination directory exists
            var destDir = Path.GetDirectoryName(destFile);
            
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(file, destFile, overwrite: true);
        }
    }

    /// <summary>
    /// Removes the specified directory path from the beginning of the source path.
    /// </summary>
    /// <param name="sourcePath">The full path from which the directory path should be removed.</param>
    /// <param name="directoryPath">The directory path to be stripped from the source path. This path must match the beginning of <paramref
    /// name="sourcePath"/>.</param>
    /// <returns>A string representing the source path without the specified directory path at the beginning. If the directory
    /// path is not at the start of the source path, the original source path is returned.</returns>
    public static string StripDirectory(this string sourcePath, string directoryPath)
    {
        return sourcePath.Replace($"{directoryPath}{Path.DirectorySeparatorChar}", "");
    }
}
