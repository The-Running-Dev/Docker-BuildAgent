using System;
using System.IO;
using System.Linq;
using Serilog;
using Nuke.Common.IO;
using Nuke.Common.Tooling;

using Extensions;
using Parameters;

namespace Utilities;

/// <summary>
/// Provides methods for building Node.js documentation apps from template repositories, including template cloning,
/// file copying, and build automation.
/// </summary>
/// <remarks>The <see cref="NodeTemplate"/> class includes static methods to automate the setup of Node.js
/// documentation projects by cloning template repositories, copying files while preserving existing customizations,
/// executing setup scripts, and building for production deployment.</remarks>
public static class NodeTemplate
{
    /// <summary>
    /// Builds a Node.js documentation app from a template repository, copying only missing files and preserving
    /// existing customizations.
    /// </summary>
    /// <param name="parameters">The parameters containing configuration for the template build process.</param>
    /// <remarks>This method automates the setup of Node.js documentation projects by:
    /// 1. Cloning a template repository from GitHub
    /// 2. Copying template files to the target directory (preserving existing files)
    /// 3. Executing setup scripts for customization
    /// 4. Auto-detecting or using specified package manager (npm, pnpm, yarn)
    /// 5. Installing dependencies and building for production if requested</remarks>
    public static void Build(NodeTemplateParams parameters)
    {
        Log.Information("🚀 Starting Node Template Build Process...");

        // Validate prerequisites
        ValidatePrerequisites();

        // Clone template repository
        CloneTemplateRepository(parameters);

        // Display build configuration
        DisplayBuildConfiguration(parameters);

        // Copy template files
        CopyTemplateFiles(parameters);

        // Execute template setup script if present
        ExecuteTemplateSetupScript(parameters);

        // Setup package management and install dependencies
        SetupPackageManagement(parameters);

        // Build for production if requested
        BuildForProduction(parameters);

        Log.Information("Node template build completed successfully");
    }

    /// <summary>
    /// Copies directory contents while preserving existing files to avoid overwriting customizations
    /// </summary>
    /// <param name="sourceDir">Source directory to copy from</param>
    /// <param name="targetDir">Target directory to copy to</param>
    private static void CopyDirectoryPreservingExisting(string sourceDir, string targetDir)
    {
        // Create target directory if it doesn't exist
        Directory.CreateDirectory(targetDir);

        // Copy all files that don't already exist in target
        foreach (string sourceFile in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            string targetFile = Path.Combine(targetDir, relativePath);
            
            // Only copy if target file doesn't exist (preserve customizations)
            if (!File.Exists(targetFile))
            {
                string targetSubDir = Path.GetDirectoryName(targetFile);
                if (!Directory.Exists(targetSubDir))
                {
                    Directory.CreateDirectory(targetSubDir);
                }
                
                File.Copy(sourceFile, targetFile);
                Log.Debug($"Copied template file: {relativePath}");
            }
            else
            {
                Log.Debug($"Skipped existing file: {relativePath}");
            }
        }
    }

    /// <summary>
    /// Validates that required prerequisites (Git, Node.js) are available.
    /// </summary>
    private static void ValidatePrerequisites()
    {
        Log.Information("🔍 Checking Prerequisites...");

        try
        {
            var gitVersion = ProcessTasks.StartProcess("git", "--version").AssertWaitForExit().Output.FirstOrDefault().Text;
            Log.Information($"   ✅ Git: {gitVersion}");
        }
        catch
        {
            throw new InvalidOperationException("❌ Git is not installed or not available in PATH. Please install Git and try again.");
        }

        try
        {
            var nodeVersion = ProcessTasks.StartProcess("node", "--version").AssertWaitForExit().Output.FirstOrDefault().Text;
            Log.Information($"   ✅ Node.js: {nodeVersion}");
        }
        catch
        {
            throw new InvalidOperationException("❌ Node.js is not installed or not available in PATH. Please install Node.js and try again.");
        }

        Log.Information("✅ Prerequisites Check Passed");
    }

    /// <summary>
    /// Clones the template repository to the specified directory.
    /// </summary>
    /// <param name="parameters">The parameters containing repository and directory information.</param>
    private static void CloneTemplateRepository(NodeTemplateParams parameters)
    {
        Log.Information("📥 Cloning Template...");
        Log.Information($"   Repository: {parameters.NodeTemplateRepositoryUrl}");
        Log.Information($"   Destination: {parameters.NodeTemplateDirPath}");

        // Clone the template repository with minimal history for faster download
        ProcessTasks.StartProcess("git", $"clone --depth 1 {parameters.NodeTemplateRepositoryUrl} {parameters.NodeTemplateDirPath}")
            .AssertZeroExitCode();

        Log.Information("✅ Template Repository Cloned Successfully");
    }

    /// <summary>
    /// Displays the current build configuration.
    /// </summary>
    /// <param name="parameters">The parameters containing build configuration.</param>
    private static void DisplayBuildConfiguration(NodeTemplateParams parameters)
    {
        Log.Information("");
        Log.Information("⚙️ Build Configuration:");
        Log.Information($"   Project Directory: {parameters.RootDirectory}");
        Log.Information($"   App Directory: {parameters.AppDir}");
        Log.Information($"   App Path: {Path.Combine(parameters.RootDirectory, parameters.AppDir)}");
        Log.Information($"   Template URL: {parameters.NodeTemplateRepositoryUrl}");
        Log.Information($"   Template Path: {parameters.NodeTemplateDirPath}");
        Log.Information($"   Artifacts Directory: {parameters.ArtifactsDir}");
        Log.Information($"   Skip Install: {parameters.SkipInstall}");
        Log.Information($"   Production Build: {parameters.IsProduction}");
        Log.Information("");
    }

    /// <summary>
    /// Copies template files to the target directory, preserving existing files.
    /// </summary>
    /// <param name="parameters">The parameters containing source and destination paths.</param>
    private static void CopyTemplateFiles(NodeTemplateParams parameters)
    {
        Log.Information("📁 Copying Template Files...");
        var appDirPath = Path.Combine(parameters.RootDirectory, parameters.AppDir);
        Log.Information($"   From: {parameters.NodeTemplateDirPath}");
        Log.Information($"   To: {appDirPath}");
        Log.Information("   Mode: Preserve Existing Files");

        // Copy template files to the target directory
        // Preserve existing files to allow customizations to persist
        CopyDirectoryPreservingExisting(parameters.NodeTemplateDirPath, appDirPath);

        Log.Information("✅ Template Files Copied Successfully");
    }

    /// <summary>
    /// Executes the template setup script if it exists.
    /// </summary>
    /// <param name="parameters">The parameters containing the working directory.</param>
    private static void ExecuteTemplateSetupScript(NodeTemplateParams parameters)
    {
        var templateSetupFile = "template-setup.ps1";
        var templateSetupFilePath = Path.Combine(parameters.RootDirectory, templateSetupFile);

        if (File.Exists(templateSetupFilePath))
        {
            Log.Information("🔧 Running Template Setup...");

            try
            {
                ProcessTasks.StartProcess("pwsh", $"-File {templateSetupFilePath}")
                    .AssertZeroExitCode();

                Log.Information("✅ Template Setup Completed");

                // Clean up the template setup script after execution
                Log.Information("🧹 Removing Template Setup Script...");
                File.Delete(templateSetupFilePath);
                Log.Information("✅ Template Setup Script Removed");
            }
            catch (Exception ex)
            {
                Log.Warning($"⚠️ Template setup script execution failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sets up package management and installs dependencies if not skipped.
    /// </summary>
    /// <param name="parameters">The parameters containing package manager and installation options.</param>
    private static void SetupPackageManagement(NodeTemplateParams parameters)
    {
        Log.Information("📦 Preparing Package Management...");
        var appDirPath = Path.Combine(parameters.RootDirectory, parameters.AppDir);

        // Change to the app directory for package manager operations
        Environment.CurrentDirectory = appDirPath;

        // Auto-detect package manager if not explicitly specified
        var packageManager = !string.IsNullOrEmpty(parameters.PackageManager)
            ? parameters.PackageManager
            : DetectPackageManager(appDirPath);

        Log.Information($"📌 Using Package Manager: {packageManager}");

        // Install dependencies unless explicitly skipped
        if (!parameters.SkipInstall)
        {
            Log.Information("⬇️ Installing Dependencies...");
            Log.Information($"   Command: {packageManager} install");

            ProcessTasks.StartProcess(packageManager, "install")
                .AssertZeroExitCode();

            Log.Information("✅ Dependencies Installed Successfully");
        }
        else
        {
            Log.Information("⏭️ Skipping Dependency Installation (SkipInstall Flag Specified)");
        }

        // Store the detected package manager for later use
        parameters.PackageManager = packageManager;
    }

    /// <summary>
    /// Builds for production if requested.
    /// </summary>
    /// <param name="parameters">The parameters containing production build options.</param>
    private static void BuildForProduction(NodeTemplateParams parameters)
    {
        if (parameters.IsProduction)
        {
            Log.Information("🏗️ Building for Production...");
            Log.Information($"   Command: {parameters.PackageManager} run build:prod");

            ProcessTasks.StartProcess(parameters.PackageManager, "run build:prod")
                .AssertZeroExitCode();

            Log.Information("✅ Production Build Completed Successfully");
        }
        else
        {
            Log.Information("⏭️ Skipping Production Build (IsProduction Set to False)");
        }
    }

    /// <summary>
    /// Detects the package manager used by a Node.js project based on lock files.
    /// </summary>
    /// <param name="projectDir">The directory path to check for package manager lock files.</param>
    /// <returns>A string representing the detected package manager (pnpm, yarn, or npm as default).</returns>
    private static string DetectPackageManager(string projectDir)
    {
        if (File.Exists(Path.Combine(projectDir, "pnpm-lock.yaml")))
        {
            return "pnpm";
        }
        if (File.Exists(Path.Combine(projectDir, "yarn.lock")))
        {
            return "yarn";
        }
        return "npm";
    }
}
