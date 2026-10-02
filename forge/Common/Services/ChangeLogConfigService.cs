#nullable enable

using System;
using Microsoft.Extensions.Logging;

using Entities;

namespace Services;

/// <summary>
/// Provides functionality to create and configure change log settings based on input criteria.
/// </summary>
public interface IChangeLogConfigService
{
    /// <summary>
    /// Creates a ChangeLogConfig based on the specified input string.
    /// </summary>
    /// <param name="input">A string representing the source of the change log. Can be null, empty, "all", or a specific tag.</param>
    /// <returns>A configured ChangeLogConfig instance.</returns>
    ChangeLogConfig Create(string? input);
}

/// <summary>
/// Service for creating ChangeLogConfig instances with proper dependency injection.
/// </summary>
/// <remarks>
/// This service encapsulates the logic for interpreting input strings and configuring
/// ChangeLogConfig objects, removing the business logic from the entity itself.
/// </remarks>
public class ChangeLogConfigService : IChangeLogConfigService
{
    private readonly IGitService _gitService;

    private readonly ILogger<ChangeLogConfigService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeLogConfigService"/> class.
    /// </summary>
    /// <param name="gitService">The GitService service for retrieving tag information.</param>
    /// <param name="logger">The logger for logging operations.</param>
    public ChangeLogConfigService(IGitService gitService, ILogger<ChangeLogConfigService> logger)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a ChangeLogConfig based on the specified input string.
    /// </summary>
    /// <param name="input">A string representing the source of the change log. Can be null, empty, "all", or a specific tag.
    /// If null or empty, the change log is generated from the last tag. If "all", the change log is generated from the entire
    /// history. Otherwise, the input is treated as a specific tag.</param>
    /// <returns>A configured ChangeLogConfig instance.</returns>
    public ChangeLogConfig Create(string? input)
    {
        var trimmed = input?.Trim();

        return trimmed?.ToLowerInvariant() switch
        {
            null or "" => CreateFromLastTag(),
            "all" => CreateFromAllHistory(),
            _ => CreateFromSpecificTag(trimmed)
        };
    }

    private ChangeLogConfig CreateFromLastTag()
    {
        _logger.LogDebug("Creating Changelog Config from Last Tag");
        
        var lastTag = _gitService.GetLastTag();
        _logger.LogInformation("Using Last Tag: {Tag}", lastTag ?? "none");
        
        return new ChangeLogConfig(ChangeLogSource.LastTag, lastTag);
    }

    private ChangeLogConfig CreateFromAllHistory()
    {
        _logger.LogDebug("Creating Changelog Config from All History");
        
        return new ChangeLogConfig(ChangeLogSource.All);
    }

    private ChangeLogConfig CreateFromSpecificTag(string tag)
    {
        _logger.LogDebug("Creating Changelog Config from Specific Tag: {Tag}", tag);

        return new ChangeLogConfig(ChangeLogSource.SpecificTag, tag);
    }
}