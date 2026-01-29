using System;
using System.Linq;
using System.Reflection;

using Nuke.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Parameters;

namespace Services;

/// <summary>
/// Decorator for IDockerService that dynamically chooses between real and simulation implementations
/// based on the current execution context's dry-run state.
/// </summary>
/// <remarks>
/// This decorator implements the Decorator pattern to provide runtime switching between
/// DockerService and DockerSimulationService based on whether dry-run mode is active.
/// The decision is made at method execution time rather than service registration time,
/// allowing proper access to the NUKE build context and parameters.
/// </remarks>
public class DockerServiceDecorator : IDockerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DockerServiceDecorator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockerServiceDecorator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving concrete implementations.</param>
    /// <param name="logger">The logger instance for logging decorator operations.</param>
    public DockerServiceDecorator(IServiceProvider serviceProvider, ILogger<DockerServiceDecorator> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes Docker registry login using the appropriate implementation based on dry-run mode.
    /// </summary>
    /// <param name="parameters">The parameters required for logging into the Docker registry.</param>
    public void Login(DockerParams parameters)
    {
        GetImplementation().Login(parameters);
    }

    /// <summary>
    /// Executes Docker image build using the appropriate implementation based on dry-run mode.
    /// </summary>
    /// <param name="parameters">The parameters used to configure the Docker build.</param>
    public void Build(DockerParams parameters)
    {
        GetImplementation().Build(parameters);
    }

    /// <summary>
    /// Executes Docker image push using the appropriate implementation based on dry-run mode.
    /// </summary>
    /// <param name="parameters">The parameters used for the Docker push operation.</param>
    public void Push(DockerParams parameters)
    {
        GetImplementation().Push(parameters);
    }

    /// <summary>
    /// Executes Docker image tagging using the appropriate implementation based on dry-run mode.
    /// </summary>
    /// <param name="parameters">The parameters containing the list of tags for the Docker image.</param>
    public void Tag(DockerParams parameters)
    {
        GetImplementation().Tag(parameters);
    }

    /// <summary>
    /// Gets the appropriate IDockerService implementation based on the current dry-run context.
    /// </summary>
    /// <returns>Either DockerService for normal execution or DockerSimulationService for dry-run mode.</returns>
    private IDockerService GetImplementation()
    {
        var isDryRun = DetectDryRunMode();
        
        if (isDryRun)
        {
            _logger.LogDebug("Dry-run mode detected - using DockerSimulationService");
            return _serviceProvider.GetRequiredService<DockerSimulationService>();
        }
        else
        {
            _logger.LogDebug("Normal mode detected - using DockerService");
            return _serviceProvider.GetRequiredService<DockerService>();
        }
    }

    /// <summary>
    /// Detects whether the current execution context is in dry-run mode.
    /// </summary>
    /// <returns>True if dry-run mode is active, false otherwise.</returns>
    private bool DetectDryRunMode()
    {
        try
        {
            // Method 1: Try to get the current NUKE build instance from static context
            var nukeBuildType = Type.GetType("Nuke.Common.NukeBuild, Nuke.Common");
            if (nukeBuildType != null)
            {
                var instanceProperty = nukeBuildType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty != null)
                {
                    var nukeInstance = instanceProperty.GetValue(null);
                    if (nukeInstance != null)
                    {
                        var dryRunProperty = nukeInstance.GetType().GetProperty("DryRun", BindingFlags.Public | BindingFlags.Instance);
                        if (dryRunProperty != null)
                        {
                            var isDryRun = (bool)(dryRunProperty.GetValue(nukeInstance) ?? false);
                            _logger.LogDebug("Dry-run detection via NukeBuild.Instance: {IsDryRun}", isDryRun);
                            return isDryRun;
                        }
                    }
                }
            }

            // Method 2: Try to get from command-line arguments in current process
            var args = Environment.GetCommandLineArgs();
            var hasDryRunArg = Array.Exists(args, arg => 
                string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase));
            
            if (hasDryRunArg)
            {
                _logger.LogDebug("Dry-run detection via command line arguments: true");
                return true;
            }

            // Method 3: Check NUKE parameters through reflection on any available build context
            try
            {
                // Look for any loaded assemblies that might contain the current build
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    var buildTypes = assembly.GetTypes().Where(t => 
                        t.IsClass && !t.IsAbstract && 
                        t.BaseType != null && 
                        t.BaseType.IsGenericType &&
                        t.BaseType.GetGenericTypeDefinition().FullName?.StartsWith("Base") == true);

                    foreach (var buildType in buildTypes)
                    {
                        // Try to find any static instance or current context
                        var staticFields = buildType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        foreach (var field in staticFields)
                        {
                            if (field.FieldType == buildType)
                            {
                                var instance = field.GetValue(null);
                                if (instance != null)
                                {
                                    var dryRunProp = instance.GetType().GetProperty("DryRun");
                                    if (dryRunProp != null)
                                    {
                                        var isDryRun = (bool)(dryRunProp.GetValue(instance) ?? false);
                                        _logger.LogDebug("Dry-run detection via build type reflection: {IsDryRun}", isDryRun);
                                        return isDryRun;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Exception during reflection-based dry-run detection: {Exception}", ex.Message);
            }

            _logger.LogDebug("No dry-run indicators found - defaulting to normal mode");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to detect dry-run mode: {Exception}. Defaulting to normal mode.", ex.Message);
            return false;
        }
    }
}
