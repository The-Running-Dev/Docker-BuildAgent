using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Nuke.Common;
using Microsoft.Extensions.Logging;

using Extensions;
using Parameters;

namespace Services;

/// <summary>
/// Simulation implementation of IDockerService that validates operations without executing them.
/// Used for dry-run scenarios to provide fast feedback without actual Docker operations.
/// </summary>
/// <remarks>
/// This service provides comprehensive validation and detailed simulation logging while avoiding
/// expensive Docker build operations. It validates all inputs, checks prerequisites, and simulates
/// the expected behavior and outcomes without performing actual Docker commands.
/// </remarks>
public class DockerSimulationService : IDockerService
{
    private readonly ILogger<DockerSimulationService> _logger;
    private readonly INodeService _nodeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockerSimulationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for logging simulation operations.</param>
    /// <param name="nodeService">The Node service for detecting application types.</param>
    public DockerSimulationService(ILogger<DockerSimulationService> logger, INodeService nodeService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nodeService = nodeService ?? throw new ArgumentNullException(nameof(nodeService));
    }

    /// <summary>
    /// Simulates Docker registry login operation with validation.
    /// </summary>
    /// <param name="parameters">The parameters required for logging into the Docker registry.</param>
    public void Login(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _logger.LogInformation("🔍 DRY-RUN: Simulating Docker registry login");
        
        ValidateLoginParameters(parameters);
        
        var server = Regex.Replace(parameters.RegistryUrl, @"/.*$", "");
        _logger.LogInformation("   🌐 Registry Server: {Server}", server);
        _logger.LogInformation("   👤 Username: {Username}", parameters.RegistryUser);
        _logger.LogInformation("   🔑 Token: {Token}", MaskToken(parameters.RegistryToken));
        _logger.LogInformation("   ✅ Login simulation completed successfully");
    }

    /// <summary>
    /// Simulates Docker image build operation with comprehensive validation.
    /// </summary>
    /// <param name="parameters">The parameters used to configure the Docker build simulation.</param>
    public void Build(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _logger.LogInformation("🔍 DRY-RUN: Simulating Docker image build");
        
        // Phase 1: Comprehensive Validation
        ValidateDockerBuild(parameters);
        
        // Phase 2: Build Simulation
        SimulateDockerBuild(parameters);
        
        // Phase 3: Simulate Tagging
        SimulateTagging(parameters);
    }

    /// <summary>
    /// Simulates Docker image push operation with validation.
    /// </summary>
    /// <param name="parameters">The parameters used for the Docker push simulation.</param>
    public void Push(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        _logger.LogInformation("🔍 DRY-RUN: Simulating Docker image push");
        
        // Simulate login first
        Login(parameters);
        
        var latestTag = parameters.Tags.FirstOrDefault(x => x.Contains("latest"));
        var versionTag = parameters.Tags.FirstOrDefault(x => !x.Contains("latest"));
        
        _logger.LogInformation("   📤 Would push: {LatestTag}", latestTag);
        _logger.LogInformation("   📤 Would push: {VersionTag}", versionTag);
        _logger.LogInformation("   ⏱️  Estimated push time: 30 seconds - 2 minutes (depending on image size and network)");
        _logger.Push("Docker Images: {Version}, latest", parameters.Version);
        _logger.LogInformation("   ✅ Push simulation completed successfully");
    }

    /// <summary>
    /// Simulates Docker image tagging operation.
    /// </summary>
    /// <param name="parameters">The parameters containing the list of tags for simulation.</param>
    public void Tag(DockerParams parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        SimulateTagging(parameters);
    }

    /// <summary>
    /// Validates Docker build parameters and prerequisites.
    /// </summary>
    private void ValidateDockerBuild(DockerParams parameters)
    {
        var validationResults = new List<string>();
        
        // Dockerfile validation
        var dockerFile = Path.Combine(parameters.RootDirectory, parameters.DockerFile);
        if (!File.Exists(dockerFile))
        {
            if (!string.IsNullOrEmpty(parameters.TemplatesDir) && Directory.Exists(parameters.TemplatesDir))
            {
                var appType = _nodeService.DetectApplicationType(parameters.RootDirectory);
                var templateDockerFile = Path.Combine(parameters.TemplatesDir, $"Dockerfile.{appType}");
                
                if (File.Exists(templateDockerFile))
                {
                    validationResults.Add($"✅ Dockerfile template available: Dockerfile.{appType}");
                    validationResults.Add($"   📄 Template path: {templateDockerFile}");
                    _logger.LogWarning("   ⚠️  Would copy template: {Template} → {Dockerfile}", templateDockerFile, dockerFile);
                }
                else
                {
                    throw new InvalidOperationException($"❌ No Dockerfile template exists for {appType}: {templateDockerFile}");
                }
            }
            else
            {
                throw new FileNotFoundException($"❌ Dockerfile not found: {dockerFile}");
            }
        }
        else
        {
            validationResults.Add($"✅ Dockerfile found: {dockerFile}");
            var dockerfileSize = new FileInfo(dockerFile).Length;
            validationResults.Add($"   📏 Dockerfile size: {dockerfileSize:N0} bytes");
        }

        // Build context validation
        if (Directory.Exists(parameters.RootDirectory))
        {
            var buildContext = new DirectoryInfo(parameters.RootDirectory);
            var contextSize = GetDirectorySize(buildContext);
            var fileCount = GetFileCount(buildContext);
            
            validationResults.Add($"✅ Build context: {parameters.RootDirectory}");
            validationResults.Add($"   📂 Context size: {FormatBytes(contextSize)}");
            validationResults.Add($"   📄 File count: {fileCount:N0} files");
            
            if (contextSize > 500_000_000) // 500MB
            {
                validationResults.Add("   ⚠️  Large build context - consider optimizing with .dockerignore");
            }
            
            // Check for .dockerignore
            var dockerIgnorePath = Path.Combine(parameters.RootDirectory, ".dockerignore");
            if (File.Exists(dockerIgnorePath))
            {
                var ignoreLines = File.ReadAllLines(dockerIgnorePath).Length;
                validationResults.Add($"   📋 .dockerignore found with {ignoreLines} entries");
            }
            else
            {
                validationResults.Add("   📋 .dockerignore not found");
            }
        }
        else
        {
            throw new DirectoryNotFoundException($"❌ Build context directory not found: {parameters.RootDirectory}");
        }

        // Tags validation
        validationResults.Add($"✅ Docker tags ({parameters.Tags.Count}):");
        foreach (var tag in parameters.Tags)
        {
            if (IsValidDockerTag(tag))
            {
                validationResults.Add($"   🏷️  {tag}");
            }
            else
            {
                throw new ArgumentException($"❌ Invalid Docker tag format: {tag}");
            }
        }

        // Docker availability simulation
        validationResults.Add("✅ Docker engine availability (assumed available in dry-run)");

        // Log all validation results
        _logger.LogInformation("   🔍 Build validation results:");
        foreach (var result in validationResults)
        {
            _logger.LogInformation("      {Result}", result);
        }
    }

    /// <summary>
    /// Simulates the Docker build process with realistic step-by-step output.
    /// </summary>
    private void SimulateDockerBuild(DockerParams parameters)
    {
        var dockerFile = Path.Combine(parameters.RootDirectory, parameters.DockerFile);
        _logger.LogInformation("   🔨 Simulating build: {DockerFile}", dockerFile);
        
        // Parse actual Dockerfile steps
        var dockerfileSteps = ParseDockerfileSteps(dockerFile);
        
        if (dockerfileSteps.Count > 0)
        {
            foreach (var step in dockerfileSteps)
            {
                if (parameters.Verbosity == Verbosity.Verbose || parameters.Verbosity == Verbosity.Normal)
                {
                    _logger.LogInformation("      {Step} ... ✅ (simulated)", step);
                }
            }
        }
        else
        {
            // Fallback to generic simulation if Dockerfile can't be read
            _logger.LogInformation("      Step 1/1 : [Dockerfile processing] ... ✅ (simulated)");
        }

        var latestTag = parameters.Tags.FirstOrDefault(x => x.Contains("latest"));
        var estimatedSize = EstimateImageSize(parameters);
        var estimatedTime = EstimateBuildTime(parameters);
        
        _logger.LogInformation("   🎯 Estimated final image size: ~{Size}MB", estimatedSize);
        _logger.LogInformation("   ⏱️  Estimated build time: {Time}", estimatedTime);
        _logger.Tag("{LatestTag}", latestTag);
        _logger.LogInformation("   ✅ Build simulation completed successfully");
    }

    /// <summary>
    /// Simulates Docker image tagging operations.
    /// </summary>
    private void SimulateTagging(DockerParams parameters)
    {
        var latestTag = parameters.Tags.FirstOrDefault(x => x.Contains("latest"));
        var versionTag = parameters.Tags.FirstOrDefault(x => !x.Contains("latest"));
        
        if (!string.IsNullOrEmpty(latestTag) && !string.IsNullOrEmpty(versionTag))
        {
            _logger.LogInformation("   🏷️  Would tag: {LatestTag} → {VersionTag}", latestTag, versionTag);
            _logger.Tag("{VersionTag}", versionTag);
        }
    }

    /// <summary>
    /// Validates Docker registry login parameters.
    /// </summary>
    private void ValidateLoginParameters(DockerParams parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.RegistryUrl))
            throw new ArgumentException("Registry URL is required for login simulation");
            
        if (string.IsNullOrWhiteSpace(parameters.RegistryUser))
            throw new ArgumentException("Registry user is required for login simulation");
            
        if (string.IsNullOrWhiteSpace(parameters.RegistryToken))
            throw new ArgumentException("Registry token is required for login simulation");
    }

    /// <summary>
    /// Validates Docker tag format according to Docker specifications.
    /// </summary>
    private static bool IsValidDockerTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        
        // Basic Docker tag validation - more comprehensive validation could be added
        var tagPattern = @"^[a-zA-Z0-9][a-zA-Z0-9._/-]*:[a-zA-Z0-9][a-zA-Z0-9._-]*$";
        return Regex.IsMatch(tag, tagPattern) && tag.Length <= 128;
    }

    /// <summary>
    /// Gets the total size of a directory and its subdirectories.
    /// </summary>
    private static long GetDirectorySize(DirectoryInfo directory)
    {
        try
        {
            return directory.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch
        {
            return 0; // Return 0 if access denied or other issues
        }
    }

    /// <summary>
    /// Gets the total file count in a directory and its subdirectories.
    /// </summary>
    private static int GetFileCount(DirectoryInfo directory)
    {
        try
        {
            return directory.GetFiles("*", SearchOption.AllDirectories).Length;
        }
        catch
        {
            return 0; // Return 0 if access denied or other issues
        }
    }

    /// <summary>
    /// Formats bytes into human-readable format.
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:N1} {suffixes[suffixIndex]}";
    }

    /// <summary>
    /// Estimates the final Docker image size based on the build context and application type.
    /// </summary>
    private int EstimateImageSize(DockerParams parameters)
    {
        try
        {
            var appType = _nodeService.DetectApplicationType(parameters.RootDirectory);
            
            // Rough estimates based on common application types
            return appType.ToLower() switch
            {
                "node" or "nodejs" => 150,      // Node.js apps ~150MB
                "angular" => 25,                 // Angular (nginx) ~25MB  
                "react" => 25,                   // React (nginx) ~25MB
                "vue" => 25,                     // Vue.js (nginx) ~25MB
                "dotnet" => 110,                 // .NET apps ~110MB
                "python" => 120,                 // Python apps ~120MB
                _ => 100                         // Default estimate
            };
        }
        catch
        {
            return 100; // Default fallback
        }
    }

    /// <summary>
    /// Estimates the build time based on the build context and application type.
    /// </summary>
    private string EstimateBuildTime(DockerParams parameters)
    {
        try
        {
            var contextSize = GetDirectorySize(new DirectoryInfo(parameters.RootDirectory));
            var appType = _nodeService.DetectApplicationType(parameters.RootDirectory);
            
            // Estimate based on context size and app type
            var baseTime = appType.ToLower() switch
            {
                "node" or "nodejs" => "2-4 minutes",    // npm install can be slow
                "angular" => "3-6 minutes",              // npm install + ng build
                "react" => "2-5 minutes",                // npm install + build
                "dotnet" => "1-3 minutes",               // dotnet restore + publish
                _ => "1-4 minutes"
            };
            
            if (contextSize > 100_000_000) // >100MB
            {
                return baseTime + " (+ time for large context)";
            }
            
            return baseTime + " (cached layers may reduce time significantly)";
        }
        catch
        {
            return "2-5 minutes"; // Default estimate
        }
    }

    /// <summary>
    /// Masks sensitive token information for logging.
    /// </summary>
    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "[not set]";
        if (token.Length <= 8) return "[hidden]";
        
        return $"{token[..4]}...{token[^4..]}";
    }

    /// <summary>
    /// Parses the Dockerfile and extracts the build steps in a shorter, more readable format.
    /// </summary>
    private static List<string> ParseDockerfileSteps(string dockerfilePath)
    {
        var steps = new List<string>();
        
        try
        {
            if (!File.Exists(dockerfilePath))
            {
                return steps;
            }

            var lines = File.ReadAllLines(dockerfilePath);
            var stepNumber = 1;
            var totalSteps = CountDockerfileInstructions(lines);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                // Handle multi-line commands (lines ending with \)
                var fullCommand = trimmedLine;
                var lineIndex = Array.IndexOf(lines, line);
                
                while (fullCommand.EndsWith("\\") && lineIndex + 1 < lines.Length)
                {
                    lineIndex++;
                    var nextLine = lines[lineIndex].Trim();
                    fullCommand = fullCommand.TrimEnd('\\') + " " + nextLine;
                }

                // Extract the Docker instruction
                var instruction = GetDockerInstruction(fullCommand);
                if (!string.IsNullOrEmpty(instruction))
                {
                    // Create a shorter version of the command for display
                    var shortCommand = ShortenDockerCommand(fullCommand);
                    steps.Add($"Step {stepNumber}/{totalSteps} : {shortCommand}");
                    stepNumber++;
                }
            }
        }
        catch (Exception)
        {
            // If parsing fails, return empty list to fall back to generic simulation
            return new List<string>();
        }

        return steps;
    }

    /// <summary>
    /// Counts the total number of Docker instructions in the Dockerfile.
    /// </summary>
    private static int CountDockerfileInstructions(string[] lines)
    {
        var count = 0;
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith("#"))
            {
                var instruction = GetDockerInstruction(trimmedLine);
                if (!string.IsNullOrEmpty(instruction))
                {
                    count++;
                }
            }
        }
        
        return count;
    }

    /// <summary>
    /// Extracts the Docker instruction from a command line.
    /// </summary>
    private static string GetDockerInstruction(string line)
    {
        var instructions = new[] { "FROM", "WORKDIR", "COPY", "ADD", "RUN", "CMD", "ENTRYPOINT", "EXPOSE", "ENV", "ARG", "USER", "LABEL", "VOLUME", "HEALTHCHECK" };
        
        foreach (var instruction in instructions)
        {
            if (line.StartsWith(instruction + " ", StringComparison.OrdinalIgnoreCase) ||
                line.Equals(instruction, StringComparison.OrdinalIgnoreCase))
            {
                return instruction;
            }
        }
        
        return string.Empty;
    }

    /// <summary>
    /// Creates a shorter, more readable version of Docker commands for simulation display.
    /// </summary>
    private static string ShortenDockerCommand(string command)
    {
        // Limit command length for display
        const int maxLength = 80;
        
        if (command.Length <= maxLength)
        {
            return command;
        }

        // Try to intelligently truncate while keeping important parts
        if (command.StartsWith("RUN", StringComparison.OrdinalIgnoreCase))
        {
            var runCommand = command[4..].Trim();
            if (runCommand.Length > maxLength - 4)
            {
                return $"RUN {runCommand[..(maxLength - 8)]}...";
            }
        }
        else if (command.StartsWith("COPY", StringComparison.OrdinalIgnoreCase))
        {
            // Keep COPY commands readable
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                return $"COPY {parts[1]} {parts[^1]}";
            }
        }

        // Generic truncation
        return command.Length > maxLength ? command[..(maxLength - 3)] + "..." : command;
    }
}
