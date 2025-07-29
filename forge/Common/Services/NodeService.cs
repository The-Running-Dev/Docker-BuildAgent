using System;
using System.IO;
using System.Text.RegularExpressions;

using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Microsoft.Extensions.Logging;
using Nuke.Common.Utilities.Collections;

using Utilities;
using Extensions;
using Parameters;

namespace Services;

public interface INodeService
{
    /// <summary>
    /// Detects the type of Node.js application based on the presence of specific configuration files and dependencies
    /// within the specified root directory.
    /// </summary>
    /// <remarks>
    /// The method checks for the presence of specific files and dependencies to determine the
    /// application type. If a <c>package.json</c> file is not found in the root directory, the method logs a warning
    /// and returns "unknown". The method logs the detected application type for informational purposes.
    /// </remarks>
    /// <param name="rootDirectory">The root directory of the application to analyze.</param>
    /// <returns>
    /// A string representing the detected application type. Possible return values include "angular", "nextjs",
    /// "nestjs", "vite", "react", "express", "node", or "unknown" if the application type cannot be determined.
    /// </returns>
    string DetectApplicationType(string rootDirectory);

    /// <summary>
    /// Detects the package manager used in the specified project directory.
    /// </summary>
    /// <remarks>
    /// The method checks for the presence of lock files specific to pnpm and yarn to determine the
    /// package manager. If a "pnpm-lock.yaml" file is found, it returns "pnpm". If a "yarn.lock" file is found, it
    /// returns "yarn". If neither file is present, it defaults to "npm".
    /// </remarks>
    /// <param name="parameters">The parameters containing the root directory of the project to inspect.</param>
    /// <returns>
    /// A string representing the detected package manager. Returns "npm" if no specific package manager
    /// lock file is found.
    /// </returns>
    string DetectPackageManager(NodeParams parameters);

    /// <summary>
    /// Executes a series of build scripts defined in the specified root directory.
    /// </summary>
    /// <remarks>
    /// The method reads a list of scripts from a file named ".build.scripts" in the specified root
    /// directory. It supports executing scripts using npm, pnpm, yarn, and PowerShell (.ps1) scripts. If no scripts are
    /// found, the method logs an error and terminates. Each script is logged before execution.
    /// </remarks>
    /// <param name="parameters">The parameters containing the root directory path where the build scripts are located.</param>
    void Build(NodeParams parameters);

    /// <summary>
    /// Copies specified files and directories from the root directory to the artifacts directory.
    /// </summary>
    /// <remarks>
    /// This method reads a list of files and directories to copy from a configuration file specified
    /// in <paramref name="parameters"/>. If a file or directory exists in the source path, it is copied to the destination path
    /// within the artifacts directory. If the source path does not exist, a warning is logged.
    /// </remarks>
    /// <param name="parameters">The parameters containing configuration and directory paths for the copy operation.</param>
    void CopyToArtifacts(NodeParams parameters);

    /// <summary>
    /// Executes a specified command with optional arguments in a given working directory.
    /// </summary>
    /// <remarks>
    /// The method logs the command and its arguments before execution. It processes the output,
    /// logging warnings and errors appropriately.
    /// </remarks>
    /// <param name="workingDirectory">The directory in which the command should be executed. Must be a valid <see cref="AbsolutePath"/>.</param>
    /// <param name="command">The command to execute. Cannot be null or empty.</param>
    /// <param name="args">Optional arguments to pass to the command. Can be null or empty.</param>
    void Run(AbsolutePath workingDirectory, string command, string args = null);
}

/// <summary>
/// Provides methods for detecting Node.js application types, package managers, and executing build scripts.
/// </summary>
/// <remarks>
/// The <see cref="NodeService"/> class includes methods to analyze Node.js projects by detecting the
/// application type based on configuration files and dependencies, identifying the package manager used, and executing
/// build scripts. It also supports copying files to an artifacts directory and running specified commands.
/// This service implementation wraps the static Node utility methods and provides dependency injection support.
/// </remarks>
public class NodeService : INodeService
{
    private readonly ILogger<NodeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NodeService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging operations.</param>
    public NodeService(ILogger<NodeService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detects the type of Node.js application based on the presence of specific configuration files and dependencies
    /// within the specified root directory.
    /// </summary>
    /// <remarks>
    /// The method checks for the presence of specific files and dependencies to determine the
    /// application type. If a <c>package.json</c> file is not found in the root directory, the method logs a warning
    /// and returns "unknown". The method logs the detected application type for informational purposes.
    /// </remarks>
    /// <param name="rootDirectory">The root directory of the application to analyze.</param>
    /// <returns>
    /// A string representing the detected application type. Possible return values include "angular", "nextjs",
    /// "nestjs", "vite", "react", "express", "node", or "unknown" if the application type cannot be determined.
    /// </returns>
    public string DetectApplicationType(string rootDirectory)
    {
        if (rootDirectory == null)
        {
            throw new ArgumentNullException(nameof(rootDirectory), "Root directory cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            _logger.LogWarning("Root directory is empty or whitespace — Unable to Detect Node App Type.");
            return "unknown";
        }

        var packageJsonPath = Path.Combine(rootDirectory, "package.json");

        if (!File.Exists(packageJsonPath))
        {
            _logger.LogWarning("package.json not Found — Unable to Detect Node App Type.");

            return "unknown";
        }

        var json = File.ReadAllText(packageJsonPath);
        var dependencies = System.Text.Json.JsonDocument.Parse(json).RootElement
            .TryGetProperty("dependencies", out var deps) ? deps.ToString() : "";

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

        _logger.LogInformation("Detected App Type: {AppType}", type);
        
        return type;
    }

    /// <summary>
    /// Detects the package manager used in the specified project directory.
    /// </summary>
    /// <remarks>
    /// The method checks for the presence of lock files specific to pnpm and yarn to determine the
    /// package manager. If a "pnpm-lock.yaml" file is found, it returns "pnpm". If a "yarn.lock" file is found, it
    /// returns "yarn". If neither file is present, it defaults to "npm".
    /// </remarks>
    /// <param name="parameters">The parameters containing the root directory of the project to inspect.</param>
    /// <returns>
    /// A string representing the detected package manager. Returns "npm" if no specific package manager
    /// lock file is found.
    /// </returns>
    public string DetectPackageManager(NodeParams parameters)
    {
        var pm = "npm";

        if (File.Exists(Path.Join(parameters.RootDirectory, "pnpm-lock.yaml")))
        {
            pm = "pnpm";
        }
        if (File.Exists(Path.Join(parameters.RootDirectory, "yarn.lock")))
        {
            pm = "yarn";
        }

        _logger.LogInformation("Detected Package Manager: {PackageManager}", pm);

        return pm;
    }

    /// <summary>
    /// Executes a series of build scripts defined in the specified root directory.
    /// </summary>
    /// <remarks>
    /// The method reads a list of scripts from a file named ".build.scripts" in the specified root
    /// directory. It supports executing scripts using npm, pnpm, yarn, and PowerShell (.ps1) scripts. If no scripts are
    /// found, the method logs an error and terminates. Each script is logged before execution.
    /// </remarks>
    /// <param name="parameters">The parameters containing the root directory path where the build scripts are located.</param>
    public void Build(NodeParams parameters)
    {
        var scripts = Files.Read(Path.Combine(parameters.RootDirectory, parameters.Config.ScriptsConfigPath));

        if (scripts == null || scripts.Count == 0)
        {
            _logger.LogInformation("No Custom Scripts Found ({ScriptsPath}), Using Convention...", 
                parameters.Config.ScriptsConfigPath.StripDirectory(parameters.RootDirectory));

            var pm = DetectPackageManager(parameters);

            scripts = [
                $"{pm} install",
                $"{pm} run build:prod"
            ];
        }

        foreach (var script in scripts)
        {
            var match = Regex.Match(script, @"^(npm|pnpm|yarn)\s+(.*)", RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var packageManager = match.Groups[1].Value;
                var command = match.Groups[2].Value;

                Run(parameters.RootDirectory, packageManager, command);

                continue;
            }

            if (script.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                var scriptPath = Path.Combine(parameters.RootDirectory, script);

                if (!File.Exists(scriptPath))
                {
                    _logger.LogError("File not Found: {ScriptPath}", scriptPath.StripDirectory(parameters.RootDirectory));
                }

                Run(parameters.RootDirectory, "pwsh", $"-NoProfile -NonInteractive -f \"{scriptPath}\"");

                continue;
            }

            Run(parameters.RootDirectory, script);
        }
    }

    /// <summary>
    /// Copies specified files and directories from the root directory to the artifacts directory.
    /// </summary>
    /// <remarks>
    /// This method reads a list of files and directories to copy from a configuration file specified
    /// in <paramref name="parameters"/>. If a file or directory exists in the source path, it is copied to the destination path
    /// within the artifacts directory. If the source path does not exist, a warning is logged.
    /// </remarks>
    /// <param name="parameters">The parameters containing configuration and directory paths for the copy operation.</param>
    public void CopyToArtifacts(NodeParams parameters)
    {
        var files = Files.Read(Path.Combine(parameters.RootDirectory, parameters.Config.FilesToCopyConfigPath));

        if (files == null || files.Count == 0)
        {
            _logger.LogInformation("Nothing to Copy ({FilesToCopyPath})...", 
                parameters.Config.FilesToCopyConfigPath.StripDirectory(parameters.RootDirectory));

            return;
        }

        foreach (var relativePath in files)
        {
            var sourcePath = Path.Combine(parameters.RootDirectory, relativePath);
            var destinationPath = Path.Combine(parameters.ArtifactsDir, relativePath);

            if (File.Exists(sourcePath))
            {
                // Ensure destination directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                _logger.Copy("Copying {SourcePath} --> {DestinationPath}", 
                    sourcePath.StripDirectory(parameters.RootDirectory), destinationPath);

                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
            else if (Directory.Exists(sourcePath))
            {
                var finalDestination = Path.Combine(
                    parameters.ArtifactsDir,
                    Path.GetFileName(relativePath) // copies to artifacts/data if src/data
                );

                _logger.Copy("Copying {SourcePath}/* --> {Destination}", 
                    sourcePath.StripDirectory(parameters.RootDirectory), finalDestination);

                sourcePath.CopyDirectory(finalDestination); // Recursive copy of contents
            }
            else
            {
                _logger.LogWarning($"Path Not Found: {sourcePath}");
            }
        }
    }

    /// <summary>
    /// Executes a specified command with optional arguments in a given working directory.
    /// </summary>
    /// <remarks>
    /// The method logs the command and its arguments before execution. It processes the output,
    /// logging warnings and errors appropriately.
    /// </remarks>
    /// <param name="workingDirectory">The directory in which the command should be executed. Must be a valid <see cref="AbsolutePath"/>.</param>
    /// <param name="command">The command to execute. Cannot be null or empty.</param>
    /// <param name="args">Optional arguments to pass to the command. Can be null or empty.</param>
    public void Run(AbsolutePath workingDirectory, string command, string args = null)
    {
        var textToShow = $"{command} {args}";
        var separator = new string('-', textToShow.Length + 4);

        _logger.LogInformation("{Separator}", separator);
        _logger.LogInformation("{CommandText}", textToShow);
        _logger.LogInformation("{Separator}", separator);

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
                    _logger.LogWarning("{OutputText}", text);
                }
                // Only log as error if it's stderr and it's meaningful
                else if (x.Type == OutputType.Err)
                {
                    _logger.LogDebug("{OutputText}", text);
                }
                else
                {
                    _logger.LogInformation("{OutputText}", text);
                }
            });
    }
}