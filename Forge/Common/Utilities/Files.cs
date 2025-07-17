using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Serilog;

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

        File.WriteAllLines(outputPath, envVars
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={kv.Value}"));

        logInfo($"✅ Environment Written to {Path.GetFileName(outputPath)}");

        return isSuccessful;
    }

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

    public class MapFileValue
    {
        public string Key { get; set; }
        
        public string Value { get; set; }
        
        public string Template { get; set; }

        public MapFileValue(string key, string template, string value)
        {
            Key = key;
            Template = template;
            Value = value;
        }
        
        public override string ToString() => $"{Key}={Value}";
    }
}