#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Entities;

using Extensions;

using Microsoft.Extensions.Logging;
//using Serilog;

using Nuke.Common.Tooling;
using Nuke.Common.Tools.Git;
using Nuke.Common.Utilities;

using Utilities;

namespace Services;

/// <summary>
/// Interface for Git operations to enable testing and dependency injection.
/// </summary>
/// <remarks>
/// This interface abstracts Git operations to enable proper testing and dependency injection.
/// Implementations should handle the actual Git command execution and provide consistent results.
/// </remarks>
public interface IGitService
{
    /// <summary>
    /// Generates a change log based on the provided configuration.
    /// </summary>
    /// <param name="config">The change log configuration specifying the source and tag.</param>
    /// <param name="options">Optional custom formatting options for customizing the output. If null, uses default formatting.</param>
    /// <returns>A formatted change log string with optional custom formatting applied.</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
    string GenerateChangeLog(ChangeLogConfig config, ChangeLogFormatOptions? options = null);

    /// <summary>
    /// Gets a list of commits since the specified tag.
    /// </summary>
    /// <param name="tag">The tag to get commits since. If null, gets all commits from the repository.</param>
    /// <returns>A list of commit information objects containing hash, author, date, and message.</returns>
    List<CommitInfo> GetCommitsSince(string? tag = null);

    /// <summary>
    /// Gets the most recent Git tag from the repository.
    /// </summary>
    /// <returns>The last tag name, or null if no tags exist in the repository.</returns>
    string? GetLastTag();

    /// <summary>
    /// Creates and pushes a new Git tag.
    /// </summary>
    /// <param name="tag">The tag name to create and push to the remote repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when tag is null.</exception>
    /// <exception cref="ArgumentException">Thrown when tag is empty or whitespace.</exception>
    void CreateTag(string tag);

    /// <summary>
    /// Gets repository, branch, and commit URLs for the Git repository.
    /// </summary>
    /// <param name="branch">The branch name to generate URLs for.</param>
    /// <param name="commit">The commit hash to generate URLs for.</param>
    /// <returns>An object containing repository, branch, and commit URLs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when branch or commit is null.</exception>
    GitUrls GetUrls(string branch, string commit);

    /// <summary>
    /// Marks the specified directory as a safe directory for Git operations.
    /// </summary>
    /// <remarks>
    /// This method configures Git to recognize the specified directory as safe, allowing operations
    /// to be performed without warnings about potential security risks. It uses the Git configuration command to add
    /// the directory to the global list of safe directories.
    /// </remarks>
    /// <param name="directoryPath">The path of the directory to be marked as safe.</param>
    /// <exception cref="ArgumentNullException">Thrown when directoryPath is null.</exception>
    /// <exception cref="ArgumentException">Thrown when directoryPath is empty or whitespace.</exception>
    void SetSafeDirectory(string directoryPath);

    /// <summary>
    /// Writes a change log to the specified file path based on the provided configuration.
    /// </summary>
    /// <param name="filePath">The file path where the change log will be written.</param>
    /// <param name="config">The change log configuration.</param>
    /// <param name="options">Optional custom formatting options. If null, uses default formatting.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath or config is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is empty or whitespace.</exception>
    void WriteChangeLog(string filePath, ChangeLogConfig config, ChangeLogFormatOptions? options = null);
}

/// <summary>
/// Default implementation of IGitService that executes actual Git commands.
/// </summary>
/// <remarks>
/// This implementation uses the Nuke framework's Git tools and process execution
/// to perform actual Git operations on the file system. It provides consistent logging
/// and error handling for all Git operations.
/// </remarks>
public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a change log based on the provided configuration.
    /// </summary>
    /// <param name="config">The change log configuration.</param>
    /// <param name="options">Optional custom formatting options. If null, uses default formatting.</param>
    /// <returns>A formatted change log string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
    public string GenerateChangeLog(ChangeLogConfig config, ChangeLogFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Use provided options or create default ones
        options ??= new ChangeLogFormatOptions();

        _logger.LogDebug($"Generating Changelog for Source: {config.Source}, Tag: {config.Tag ?? "null"}");

        var commits = GetCommitsSince(config.Tag);
        var result = ChangeLogFormatter.Format(commits, config, options);

        _logger.LogInformation($"Generated Changelog with {commits.Count} Commits");

        return result;
    }

    /// <summary>
    /// Writes a change log to the specified file path based on the provided configuration.
    /// </summary>
    /// <param name="filePath">The file path where the change log will be written.</param>
    /// <param name="config">The change log configuration.</param>
    /// <param name="options">Optional custom formatting options. If null, uses default formatting.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath or config is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is empty or whitespace.</exception>
    public void WriteChangeLog(string filePath, ChangeLogConfig config, ChangeLogFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(config);
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File Path Cannot Be Empty or Whitespace.", nameof(filePath));
        }
        
        _logger.LogInformation($"Writing Changelog to: {filePath}");

        var changeLog = GenerateChangeLog(config, options);

        if (string.IsNullOrEmpty(changeLog))
        {
            _logger.LogWarning("Changelog is Empty, Nothing to Write");

            return;
        }

        var content = string.Empty;
        if (File.Exists(filePath))
        {
            content = File.ReadAllText(filePath);
            content += "\n" + changeLog;

            _logger.LogDebug("Appending to Existing Changelog File");
        }
        else
        {
            content = changeLog;

            _logger.LogDebug("Creating New Changelog File");
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            
            _logger.LogDebug($"Created Directory: {directory}");
        }

        File.WriteAllText(filePath, content);
        
        _logger.LogInformation("Successfully Wrote Changelog to: {FilePath}", filePath);
    }

    /// <summary>
    /// Gets a list of commits since the specified tag.
    /// </summary>
    /// <param name="tag">The tag to get commits since. If null, gets all commits.</param>
    /// <returns>A list of commit information.</returns>
    public List<CommitInfo> GetCommitsSince(string? tag = null)
    {
        _logger.LogDebug("Getting Commits Since: {Tag}", tag ?? "beginning");
        
        var args = new List<string> { "log" };

        if (!string.IsNullOrWhiteSpace(tag))
        {
            args.Add($"{tag}..HEAD");
        }

        args.AddRange([
            "--pretty=format:%h%x09%an%x09%ad%x09%s",
            "--date=short"
        ]);

        try
        {
            var output = ProcessTasks.StartProcess("git", args.Join(" "), logOutput: false, logInvocation: false)
                .AssertZeroExitCode()
                .Output
                .Select(x => x.Text)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var commits = output
                .Select(line =>
                {
                    var parts = line.Split('\t');
                    if (parts.Length < 4)
                    {
                        _logger.LogWarning("Malformed Git Log Line: {Line}", line);
                        return null;
                    }
                    
                    return new CommitInfo
                    {
                        Hash = parts[0],
                        Author = parts[1],
                        Date = parts[2],
                        Message = parts[3]
                    };
                })
                .Where(commit => commit != null)
                .Cast<CommitInfo>()
                .ToList();

            _logger.LogInformation("Retrieved {CommitCount} Commits Since {Tag}", commits.Count, tag ?? "beginning");
            return commits;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to Get Commits Since {Tag}", tag ?? "beginning");
            throw;
        }
    }

    /// <summary>
    /// Gets the most recent Git tag.
    /// </summary>
    /// <returns>The last tag name, or null if no tags exist.</returns>
    public string? GetLastTag()
    {
        _logger.LogDebug("Getting Last Git Tag");
        
        try
        {
            var process = ProcessTasks.StartProcess("git", "describe --tags --abbrev=0", logOutput: false, logInvocation: false);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _logger.LogDebug("No Tags Found in Repository");
                return null;
            }

            var result = process.Output
                .Select(x => x.Text)
                .FirstOrDefault()?
                .Trim();

            _logger.LogInformation("Last Tag: {Tag}", result ?? "none");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to Get Last Tag");
            throw;
        }
    }

    /// <summary>
    /// Creates and pushes a new Git tag.
    /// </summary>
    /// <param name="tag">The tag name to create.</param>
    /// <exception cref="ArgumentNullException">Thrown when tag is null.</exception>
    /// <exception cref="ArgumentException">Thrown when tag is empty or whitespace.</exception>
    public void CreateTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag Cannot Be Empty or Whitespace.", nameof(tag));
        }

        _logger.LogInformation("Creating and Pushing Tag: {Tag}", tag);
        
        try
        {
            var existingTags = GitTasks.Git("tag");

            if (existingTags.All(l => l.Text != tag))
            {
                GitTasks.Git($"tag -f {tag}");
                GitTasks.Git($"push origin -f {tag}");

                _logger.Tag("Created and Pushed Tag: {Tag}", tag);
            }
            else
            {
                _logger.Ok("Tag {Tag} Already Exists", tag);
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorStatus(ex, "Failed to Create Tag: {Tag}", tag);
            throw;
        }
    }

    /// <summary>
    /// Gets repository, branch, and commit URLs for the Git repository.
    /// </summary>
    /// <param name="branch">The branch name.</param>
    /// <param name="commit">The commit hash.</param>
    /// <returns>An object containing repository, branch, and commit URLs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when branch or commit is null.</exception>
    public GitUrls GetUrls(string branch, string commit)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(commit);

        _logger.LogDebug("Getting URLs for Branch: {Branch}, Commit: {Commit}", branch, commit);

        string? repoUrl = null;

        try
        {
            var remoteUrlOutput = ProcessTasks.StartProcess("git", "config --get remote.origin.url", logOutput: false, logInvocation: false)
                .AssertZeroExitCode()
                .Output;
            var remoteUrl = remoteUrlOutput.Select(x => x.Text?.Trim()).FirstOrDefault(x => !string.IsNullOrEmpty(x));

            if (!string.IsNullOrEmpty(remoteUrl))
            {
                // Convert SSH or HTTPS to web URL
                if (remoteUrl.StartsWith("git@"))
                {
                    // git@github.com:owner/repo.git => https://github.com/owner/repo
                    repoUrl = "https://" + remoteUrl.Substring(4).Replace(":", "/").Replace(".git", "");
                }
                else if (remoteUrl.StartsWith("https://"))
                {
                    repoUrl = remoteUrl.Replace(".git", "");
                }

                _logger.LogDebug("Repository URL: {RepoUrl}", repoUrl);
            }
            else
            {
                _logger.LogWarning("Could not Determine Repository URL");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to Get Remote URL");
        }

        var branchUrl = !string.IsNullOrEmpty(repoUrl) && branch != "unknown" ? $"{repoUrl}/tree/{branch}" : null;
        var commitUrl = !string.IsNullOrEmpty(repoUrl) && commit != "unknown" ? $"{repoUrl}/commit/{commit}" : null;

        var result = new GitUrls
        {
            Repository = repoUrl,
            Branch = branchUrl,
            Commit = commitUrl
        };

        _logger.LogInformation("Generated URLs - Repository: {Repository}, Branch: {Branch}, Commit: {Commit}", 
            result.Repository ?? "null", result.Branch ?? "null", result.Commit ?? "null");

        return result;
    }

    /// <summary>
    /// Marks the specified directory as a safe directory for Git operations.
    /// </summary>
    /// <remarks>This method configures Git to recognize the specified directory as safe, allowing operations
    /// to be performed without warnings about potential security risks. It uses the Git configuration command to add
    /// the directory to the global list of safe directories.</remarks>
    /// <param name="directoryPath">The path of the directory to be marked as safe. Cannot be null or empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when directoryPath is null.</exception>
    /// <exception cref="ArgumentException">Thrown when directoryPath is empty or whitespace.</exception>
    public void SetSafeDirectory(string directoryPath)
    {
        ArgumentNullException.ThrowIfNull(directoryPath);
        
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory Cannot Be Empty or White Space.", nameof(directoryPath));
        }
        
        try
        {
            ProcessTasks.StartProcess("git", $"config --global --add safe.directory \"{directoryPath}\"", logOutput: false, logInvocation: false)
                .AssertZeroExitCode();
            
            _logger.Ok($"Set Git Safe Directory: {directoryPath}");
        }
        catch (Exception ex)
        {
            _logger.ErrorStatus(ex, $"Failed to Set Git Safe Directory: {directoryPath}");

            throw;
        }
    }
}