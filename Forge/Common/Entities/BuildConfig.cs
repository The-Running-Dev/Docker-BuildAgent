using System.IO;

using Nuke.Common.IO;

namespace Entities;

/// <summary>
/// Represents the configuration for build-related operations, including directory paths and file settings.
/// </summary>
/// <param name="rootDirectory">The root directory of the project. This is used as the base path for constructing other paths.</param>
public class BuildConfig(AbsolutePath rootDirectory)
{
    public string Directory = ".build";

    public string FilesToCopyConfig = ".build.copy";

    public string ScriptsConfig = ".build.scripts";

    public string EnvFile = ".build.env";

    public string EnvMapFile = ".build.env.map";
    
    public string AppEnvFile = ".env";

    public string AppEnvMapFile = ".app.env.map";

    public string DirectoryPath => Path.Combine(Path.Combine(rootDirectory, Directory));

    public string FilesToCopyConfigPath => Path.Combine(DirectoryPath, FilesToCopyConfig);

    public string ScriptsConfigPath => Path.Combine(DirectoryPath, ScriptsConfig);

    public string EnvFilePath => Path.Combine(DirectoryPath, EnvFile);

    public string EnvMapFilePath => Path.Combine(DirectoryPath, EnvMapFile);

    public string AppEnvFilePath => Path.Combine(rootDirectory, AppEnvFile);

    public string AppEnvMapFilePath => Path.Combine(DirectoryPath, AppEnvMapFile);

    // GitHub tokens
    // base URLs
    // rootDirectory\\
    public string[] StripForDisplay =>
    [
        @"ghp_[\w]+",                                           
        @"https?://[^/]+",                                      
        $"{rootDirectory.ToString().Replace(@"\", @"\\")}{new string(Path.DirectorySeparatorChar, 2)}"
    ];
}