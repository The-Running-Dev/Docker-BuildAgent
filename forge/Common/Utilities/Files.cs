using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Serilog;

using Entities;

namespace Utilities;

/// <summary>
/// Provides utility methods for file operations.
/// </summary>
public static class Files
{
    /// <summary>
    /// Reads all non-empty lines from a specified file, trimming whitespace from each line.
    /// </summary>
    /// <param name="filePath">The path to the file to read. Must not be null or empty.</param>
    /// <returns>A list of strings containing the trimmed, non-empty lines from the file. Returns an empty list if the file does
    /// not exist.</returns>
    public static List<string> Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        return File.ReadAllLines(filePath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();
    }

    /// <summary>
    /// Generates an environment file from a specified map file and writes it to the given output path.
    /// </summary>
    /// <remarks>This method parses the environment variables from the specified map file and writes them to
    /// the output path. It logs the creation of directories and any warnings about empty environment
    /// variables.</remarks>
    /// <param name="mapFilePath">The path to the map file containing environment variable definitions.</param>
    /// <param name="outputPath">The path where the generated environment file will be written.</param>
    /// <param name="logInfo">An optional action to log informational messages. If not provided, a default logger is used.</param>
    /// <param name="logWarning">An optional action to log warning messages. If not provided, a default logger is used.</param>
    /// <returns><see langword="true"/> if the environment file is generated successfully; otherwise, <see langword="false"/> if
    /// any environment variable is empty.</returns>
    public static bool GenerateEnvironmentFile(string mapFilePath, string outputPath, Action<string> logInfo = null, Action<string> logWarning = null)
    {
        var isSuccessful = true;
        var envVars = ParseEnvironment(mapFilePath);

        // Default to Nuke's Log if no delegate is provided
        logInfo ??= Log.Information;
        logWarning ??= Log.Warning;

        foreach (var mapFileValue in envVars)
        {
            if (string.IsNullOrEmpty(mapFileValue.Value))
            {
                logWarning($"⚠️ {mapFileValue.Key} is Empty, Set {mapFileValue.Template}");
                isSuccessful = false;
            }
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);

            logInfo($"✅ Created Directory: {directory}");
        }

        File.WriteAllLines(outputPath, envVars
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={EscapeValue(kv.Value)}"));

        logInfo($"✅ Environment Written to {Path.GetFileName(outputPath)}");

        return isSuccessful;
    }

    /// <summary>
    /// Parses a map file to extract key-value pairs, resolving values from constants or environment variables.
    /// </summary>
    /// <remarks>Each line in the map file should be in the format "key=value". Lines starting with "#" or
    /// containing only whitespace are ignored. Values can be prefixed with "const:" to use a constant value or "env:"
    /// to resolve the value from an environment variable. If no prefix is provided, the method attempts to resolve the
    /// value as an environment variable.</remarks>
    /// <param name="mapFilePath">The path to the map file containing key-value definitions.</param>
    /// <returns>A list of <see cref="MapFileValue"/> objects representing the parsed key-value pairs. Returns an empty list if
    /// the file does not exist or contains no valid entries.</returns>
    public static List<MapFileValue> ParseEnvironment(string mapFilePath)
    {
        if (!File.Exists(mapFilePath))
        {
            return [];
        }

        var result = new List<MapFileValue>();

        foreach (var line in File.ReadAllLines(mapFilePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
            {
                continue;
            }

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var targetKey = parts[0].Trim();
            var template = parts[1].Trim();

            string value;
            if (template.StartsWith("const:"))
            {
                value = template.Substring("const:".Length);
            }
            else if (template.StartsWith("env:"))
            {
                var envVar = template.Substring("env:".Length);
                value = Environment.GetEnvironmentVariable(envVar);
            }
            else
            {
                value = Environment.GetEnvironmentVariable(template);
            }

            result.Add(new MapFileValue(targetKey, template, value));
        }

        return result;
    }
    
    /// <summary>
    /// Escapes a value for safe use in an environment (.env) file.
    /// Wraps the value in double quotes if it contains spaces, special characters, or newlines, and escapes embedded quotes.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value, suitable for .env files.</returns>
    public static string EscapeValue(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        // Escape embedded double quotes
        var escaped = value.Replace("\"", "\\\"");

        // If value contains whitespace, #, =, or newlines, wrap in double quotes
        if (escaped.Any(c => char.IsWhiteSpace(c)) || escaped.Contains('#') || escaped.Contains('=') || escaped.Contains('\n') || escaped.Contains('\r'))
        {
            return $"\"{escaped}\"";
        }

        return escaped;
    }
}