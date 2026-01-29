using System.IO;

using Serilog;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;

using Extensions;
using Parameters;

namespace Utilities;

/// <summary>
/// Provides methods for detecting Node.js application types, package managers, and executing build scripts.
/// </summary>
/// <remarks>The <see cref="Node"/> class includes static methods to analyze Node.js projects by detecting the
/// application type based on configuration files and dependencies, identifying the package manager used, and executing
/// build scripts. It also supports copying files to an artifacts directory and running specified commands.</remarks>
public static class Node
{
    /// <summary>
    /// Detects the type of Node.js application based on the presence of specific configuration files and dependencies
    /// within the specified root directory.
    /// </summary>
    /// <remarks>The method checks for the presence of specific files and dependencies to determine the
    /// application type. If a <c>package.json</c> file is not found in the root directory, the method logs a warning
    /// and returns "unknown". The method logs the detected application type for informational purposes.</remarks>
    /// <param name="rootDirectory">The root directory of the application to analyze.</param>
    /// <returns>A string representing the detected application type. Possible return values include "angular", "nextjs",
    /// "nestjs", "vite", "react", "express", "node", or "unknown" if the application type cannot be determined.</returns>
    public static string DetectApplicationType(string rootDirectory)
    {
        var packageJsonPath = Path.Combine(rootDirectory, "package.json");

        if (!File.Exists(packageJsonPath))
        {
            Log.Warning("package.json not Found — Unable to Detect Node App Type.");

            return "unknown";
        }

        var json = File.ReadAllText(packageJsonPath);
        string dependencies;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            dependencies = doc.RootElement.TryGetProperty("dependencies", out var deps) ? deps.ToString() : "";
        }
        catch
        {
            Log.Warning("package.json is Invalid JSON — Unable to Detect Node App Type.");
            return "unknown";
        }

        var type = "unknown";

        if (File.Exists(Path.Combine(rootDirectory, "angular.json")))
        {
            type = "angular";
        }
        else if (File.Exists(Path.Combine(rootDirectory, "next.config.js")) || dependencies.Contains("\"next\""))
        {
            type = "nextjs";
        }
        else if (File.Exists(Path.Combine(rootDirectory, "nest-cli.json")) || dependencies.Contains("\"@nestjs/core\""))
        {
            type = "nestjs";
        }
        else if (File.Exists(Path.Combine(rootDirectory, "vite.config.ts")) || File.Exists(Path.Combine(rootDirectory, "vite.config.js")))
        {
            type = "vite";
        }
        else if (dependencies.Contains("\"react-scripts\""))
        {
            type = "react";
        }
        else if (dependencies.Contains("\"express\""))
        {
            type = "express";
        }
        else if (File.Exists(Path.Combine(rootDirectory, "tsconfig.json")))
        {
            type = "node";
        }

        Log.Information($"Detected App Type: {type}");
        
        return type;
    }

    /// <summary>
    /// Detects the package manager used in the specified project directory.
    /// </summary>
    /// <remarks>The method checks for the presence of lock files specific to pnpm and yarn to determine the
    /// package manager. If a "pnpm-lock.yaml" file is found, it returns "pnpm". If a "yarn.lock" file is found, it
    /// returns "yarn". If neither file is present, it defaults to "npm".</remarks>
    /// <param name="p">The parameters containing the root directory of the project to inspect.</param>
    /// <returns>A string representing the detected package manager. Returns "npm" if no specific package manager
    /// lock file is found.</returns>
    public static string DetectPackageManager(NodeParams p)
    {
        var pm = "npm";

        if (File.Exists(Path.Join(p.RootDirectory, "pnpm-lock.yaml")))
        {
            pm = "pnpm";
        }
        else if (File.Exists(Path.Join(p.RootDirectory, "yarn.lock")))
        {
            pm = "yarn";
        }

        Log.Information($"Detected Package Manager: {pm}");

        return pm;
    }

    /// <summary>
    /// Executes a series of build scripts defined in the specified root directory.
    /// </summary>
    /// <remarks>The method reads a list of scripts from a file named ".build.scripts" in the specified root
    /// directory. It supports executing scripts using npm, pnpm, yarn, and PowerShell (.ps1) scripts. If no scripts are
    /// found, the method logs an error and terminates. Each script is logged before execution.</remarks>
    /// <param name="p">The parameters containing the root directory path where the build scripts are located.</param>
    public static void Build(NodeParams p)
    {
        var scripts = Files.Read(Path.Combine(p.RootDirectory, p.Config.ScriptsConfigPath));

        if (scripts == null || scripts.Count == 0)
        {
            Log.Information(($"No Custom Scripts Found ({p.Config.ScriptsConfigPath.StripDirectory(p.RootDirectory)}), Using Convention..."));

            var pm = DetectPackageManager(p);

            scripts = [
                $"{pm} install",
                $"{pm} run build:prod"
            ];
        }

        foreach (var script in scripts)
        {
            if (script.TryParsePackageManagerCommand(out var packageManager, out var command))
            {
                Run(p.RootDirectory, packageManager, command);

                continue;
            }

            if (script.EndsWith(".ps1", System.StringComparison.OrdinalIgnoreCase))
            {
                var scriptPath = Path.Combine(p.RootDirectory, script);

                if (!File.Exists(scriptPath))
                {
                    Log.Error($"File not Found: {scriptPath.StripDirectory(p.RootDirectory)}");
                    
                    continue;
                }

                Run(p.RootDirectory, "pwsh", $"-NoProfile -NonInteractive -f \"{scriptPath}\"");

                continue;
            }

            Run(p.RootDirectory, script);
        }
    }

    /// <summary>
    /// Copies specified files and directories from the root directory to the artifacts directory.
    /// </summary>
    /// <remarks>This method reads a list of files and directories to copy from a configuration file specified
    /// in <paramref name="p"/>. If a file or directory exists in the source path, it is copied to the destination path
    /// within the artifacts directory. If the source path does not exist, a warning is logged.</remarks>
    /// <param name="p">The parameters containing configuration and directory paths for the copy operation.</param>
    public static void CopyToArtifacts(NodeParams p)
    {
        var files = Files.Read(Path.Combine(p.RootDirectory, p.Config.FilesToCopyConfigPath));

        if (files == null || files.Count == 0)
        {
            Log.Information($"Nothing to Copy ({p.Config.FilesToCopyConfigPath.StripDirectory(p.RootDirectory)})...");
            return;
        }

        foreach (var relativePath in files)
        {
            var sourcePath = Path.Combine(p.RootDirectory, relativePath);
            var destinationPath = Path.Combine(p.ArtifactsDir, relativePath);

            if (File.Exists(sourcePath))
            {
                // Ensure destination directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                Log.Information($"📄 Copying {sourcePath.StripDirectory(p.RootDirectory)} --> {destinationPath}");

                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            else if (Directory.Exists(sourcePath))
            {
                var finalDestination = Path.Combine(
                    p.ArtifactsDir,
                    Path.GetFileName(relativePath) // copies to artifacts/data if src/data
                );

                Log.Information($"📁 Copying {sourcePath.StripDirectory(p.RootDirectory)}/* --> {finalDestination}");

                sourcePath.CopyDirectory(finalDestination); // Recursive copy of contents
            }
            else
            {
                Log.Warning($"⚠️ Path Not Found: {sourcePath}");
            }
        }
    }

    /// <summary>
    /// Executes a specified command with optional arguments in a given working directory.
    /// </summary>
    /// <remarks>The method logs the command and its arguments before execution. It processes the output,
    /// logging warnings and errors appropriately.</remarks>
    /// <param name="workingDirectory">The directory in which the command should be executed. Must be a valid <see cref="AbsolutePath"/>.</param>
    /// <param name="command">The command to execute. Cannot be null or empty.</param>
    /// <param name="args">Optional arguments to pass to the command. Can be null or empty.</param>
    public static void Run(AbsolutePath workingDirectory, string command, string args = null)
    {
        var textToShow = $"{command} {args}";
        var separator = new string('-', textToShow.Length + 4);

        Log.Information(separator);
        Log.Information(textToShow);
        Log.Information(separator);

        ProcessTasks
            .StartProcess(command, args, workingDirectory: workingDirectory, logOutput: false, logInvocation: false)
            .AssertZeroExitCode()
            .Output.ForEach(x =>
            {
                var text = x.Text?.Trim();

                // Skip all empty or whitespace lines regardless of OutputType
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                var lower = text.ToLowerInvariant();

                // Known warning patterns
                if (lower.Contains("warn"))
                {
                    Log.Warning(text);
                }
                // Only log as error if it's stderr and it's meaningful
                else if (x.Type == OutputType.Err)
                {
                    Log.Debug(text);
                }
                else
                {
                    Log.Information(text);
                }
            });
    }
}
