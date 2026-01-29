using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Extensions;

/// <summary>
/// Extension methods for string sanitization and log-safe formatting.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Extracts the registry server/host from a registry URL or path-like value.
    /// </summary>
    /// <param name="registryUrl">Registry URL or host string.</param>
    /// <returns>The registry host/server value.</returns>
    public static string GetRegistryServer(this string registryUrl)
    {
        if (string.IsNullOrWhiteSpace(registryUrl))
        {
            return registryUrl;
        }

        if (Uri.TryCreate(registryUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return registryUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? registryUrl;
    }

    /// <summary>
    /// Parses a package manager command line (npm/pnpm/yarn) into its parts.
    /// </summary>
    /// <param name="script">Script line to parse.</param>
    /// <param name="packageManager">Detected package manager.</param>
    /// <param name="command">Command portion after the package manager.</param>
    /// <returns>True when the script matches a package manager command.</returns>
    public static bool TryParsePackageManagerCommand(this string script, out string packageManager, out string command)
    {
        packageManager = null;
        command = null;

        if (string.IsNullOrWhiteSpace(script))
        {
            return false;
        }

        var match = Regex.Match(script, @"^(npm|pnpm|yarn)\s+(.*)", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        packageManager = match.Groups[1].Value;
        command = match.Groups[2].Value;

        return true;
    }

    /// <summary>
    /// Normalizes a GitHub repository URL into an owner/repo slug.
    /// </summary>
    /// <param name="repositoryUrl">Repository URL or slug.</param>
    /// <returns>Owner/repo slug when possible, otherwise the input value.</returns>
    public static string GetGitHubRepoSlug(this string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return repositoryUrl;
        }

        var repoPath = Regex.Replace(repositoryUrl, @"(https?:\/\/)?(www\.)?(ghcr\.io|github\.com)\/", "")
            .Trim('/')
            .Replace(".git", "");

        var repoParts = repoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return repoParts.Length >= 2 ? $"{repoParts[0]}/{repoParts[1]}" : repoPath;
    }

    /// <summary>
    /// Sanitizes a string for log output by flattening newlines and truncating length.
    /// </summary>
    /// <param name="value">The string to sanitize.</param>
    /// <param name="maxLength">Maximum length to keep before truncation.</param>
    /// <param name="emptyPlaceholder">Placeholder when the value is null or whitespace.</param>
    /// <returns>A log-safe string.</returns>
    public static string SanitizeForLog(this string value, int maxLength = 500, string emptyPlaceholder = "(no error body)")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return emptyPlaceholder;
        }

        var sanitized = value.Replace("\r", " ").Replace("\n", " ");

        if (maxLength > 0 && sanitized.Length > maxLength)
        {
            sanitized = sanitized[..maxLength] + "...";
        }

        return sanitized;
    }
}
