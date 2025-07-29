---
id: dependency-injection
title: 🔧 Dependency Injection
sidebar_position: 2
---

The Forge build system includes a comprehensive dependency injection container that manages all services, making the code more testable, maintainable, and following modern .NET best practices.

## Overview

The Forge build system now includes a comprehensive dependency injection container that manages all services including:

- **IGitService**: Git operations and repository management
- **IGitHubService**: GitHub API operations and release management
- **IDockerService**: Docker operations and container management
- **INodeService**: Node.js operations and package management
- **INotifications**: Build notification services
- **ILogger**: Logging services (via Microsoft.Extensions.Logging)

## Key Components

### 1. ServiceCollectionExtensions

Located in `Common/DependencyInjection/ServiceCollectionExtensions.cs`, this class provides extension methods for configuring services:

```csharp
var services = new ServiceCollection();
services.AddForgeServices(); // Adds core Forge services
services.AddNotificationServices<MyNotifications>(); // Adds notification services
```

### 2. ServiceLocator

Located in `Common/DependencyInjection/ServiceLocator.cs`, this provides a service locator pattern for backward compatibility:

```csharp
// Initialize once at application startup
ServiceLocator.InitializeWithDefaultServices<NoNotifications>();

// Get services anywhere in your code
var gitService = ServiceLocator.GetRequiredService<IGitService>();
```

### 3. Base Class Integration

The `Base<TParams, TNotifications>` class automatically sets up dependency injection:

```csharp
public class MyBuild : Base<MyParams, MyNotifications>
{
    // Services are automatically available as properties
    // this.Git - IGitService instance
    // this.GitHub - IGitHubService instance
    // this.Docker - IDockerService instance
    // this.Node - INodeService instance
    // this.NotificationService - TNotifications instance
}
```

## Usage Patterns

### Pattern 1: Basic Build Class (Recommended)

Create your build class by inheriting from `Base<TParams, TNotifications>`:

```csharp
public class MyBuild : Base<DockerParams, DiscordNotifications>
{
    public Target BuildTarget => _ => _
        .Executes(async () =>
        {
            // Use services directly as properties
            var currentBranch = await Git.GetCurrentBranchAsync();
            var repositoryUrl = await Git.GetRepositoryUrlAsync();
            
            // Docker operations
            await Docker.BuildImageAsync("Dockerfile", "myapp", new[] { "latest" });
            
            // GitHub operations
            await GitHub.CreateReleaseAsync("v1.0.0", "Release v1.0.0", "Release notes");
            
            // Node.js operations
            var packageManager = await Node.DetectPackageManagerAsync(".");
            await Node.InstallDependenciesAsync(".", packageManager);
        });
}
```

### Pattern 2: Custom Service Registration

Override `ConfigureServices` to add your own services:

```csharp
public class MyBuild : Base<DockerParams, DiscordNotifications>
{
    protected override void ConfigureServices(IServiceCollection services)
    {
        // Add your custom services
        services.AddSingleton<IMyCustomService, MyCustomService>();
        services.AddScoped<IAnotherService, AnotherService>();
        
        // Override existing services if needed
        services.AddSingleton<IGitService, MyCustomGitService>();
    }
    
    public Target BuildTarget => _ => _
        .Executes(() =>
        {
            // Get custom services from the container
            var customService = ServiceProvider.GetRequiredService<IMyCustomService>();
        });
}
```

### Pattern 3: Service Locator (For Legacy Code)

Use the service locator pattern in static methods or legacy code:

```csharp
public static class LegacyBuildHelper
{
    static LegacyBuildHelper()
    {
        // Initialize once
        if (!ServiceLocator.IsInitialized)
        {
            ServiceLocator.InitializeWithDefaultServices<NoNotifications>();
        }
    }
    
    public static async Task DoSomethingAsync()
    {
        var gitService = ServiceLocator.GetRequiredService<IGitService>();
        var currentBranch = await gitService.GetCurrentBranchAsync();
        // Use the service...
    }
}
```

### Pattern 4: Manual Container Setup

For advanced scenarios, manually configure the container:

```csharp
var services = new ServiceCollection();

// Add Forge services
services.AddForgeServices();
services.AddNotificationServices<MyNotifications>();

// Add logging with custom configuration
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddFile("/logs/build.log");
    builder.SetMinimumLevel(LogLevel.Debug);
});

// Add custom services
services.AddSingleton<IMyService, MyService>();

var serviceProvider = services.BuildServiceProvider();

// Use services
var gitService = serviceProvider.GetRequiredService<IGitService>();
```

## Service Interfaces

### IGitService

Handles Git operations including repository management, tagging, and commit operations:

```csharp
public interface IGitService
{
    Task<string> GetCurrentBranchAsync();
    Task<bool> CreateTagAsync(string tagName, string message);
    Task<bool> PushTagAsync(string tagName);
    Task<string> GetRepositoryUrlAsync();
}
```

### IGitHubService

Manages GitHub API operations for releases and repository interactions:

```csharp
public interface IGitHubService
{
    Task<bool> CreateReleaseAsync(string tagName, string releaseName, string body);
    Task<bool> UploadReleaseAssetAsync(string releaseId, string filePath);
    Task<Repository> GetRepositoryAsync(string owner, string name);
}
```

### IDockerService

Handles Docker operations including image building, tagging, and registry operations:

```csharp
public interface IDockerService
{
    Task<bool> BuildImageAsync(string dockerfilePath, string imageName, string[] tags);
    Task<bool> PushImageAsync(string imageName, string registry);
    Task<bool> TagImageAsync(string sourceImage, string targetImage);
}
```

### INodeService

Manages Node.js operations including package management and build processes:

```csharp
public interface INodeService
{
    Task<string> DetectPackageManagerAsync(string workingDirectory);
    Task<bool> InstallDependenciesAsync(string workingDirectory, string packageManager);
    Task<bool> RunBuildScriptAsync(string workingDirectory, string script);
}
```

## Service Lifetimes

The default service registrations use the following lifetimes:

- **IGitService**: Singleton (one instance per container)
- **IGitHubService**: Singleton (one instance per container)
- **IDockerService**: Singleton (one instance per container)
- **INodeService**: Singleton (one instance per container)
- **INotifications**: Singleton (one instance per container)
- **ILogger**: Singleton (configured by Microsoft.Extensions.Logging)

You can override these when registering custom services:

```csharp
services.AddScoped<IGitService, MyGitService>(); // New instance per scope
services.AddTransient<IGitService, MyGitService>(); // New instance every time
```

## Testing with Dependency Injection

The DI system makes testing much easier:

```csharp
[Test]
public async Task TestBuildProcess()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddTransient<IGitService, MockGitService>();
    services.AddTransient<IDockerService, MockDockerService>();
    services.AddTransient<ILogger<MyBuild>, MockLogger<MyBuild>>();
    
    var serviceProvider = services.BuildServiceProvider();
    
    var build = new MyBuild();
    build.SetServiceProvider(serviceProvider);
    
    // Act
    var result = await build.ExecuteAsync();
    
    // Assert
    Assert.That(result, Is.True);
}
```

### Mock Service Example

```csharp
public class MockGitService : IGitService
{
    public Task<string> GetCurrentBranchAsync() => Task.FromResult("main");
    
    public Task<bool> CreateTagAsync(string tagName, string message) => Task.FromResult(true);
    
    public Task<bool> PushTagAsync(string tagName) => Task.FromResult(true);
    
    public Task<string> GetRepositoryUrlAsync() => Task.FromResult("https://github.com/test/repo");
}
```

## Migration Guide

### Migrating from Static Services

**Before:**

```csharp
// Old static usage
var commits = GitService.GetCommitsSince("v1.0.0");
var release = GitHubService.CreateRelease(parameters);
```

**After:**

```csharp
// New DI usage in build classes
var commits = await Git.GetCommitsSinceAsync("v1.0.0");
var release = await GitHub.CreateReleaseAsync(parameters);

// Or using service locator in static contexts
var gitService = ServiceLocator.GetRequiredService<IGitService>();
var commits = await gitService.GetCommitsSinceAsync("v1.0.0");
```

### Updating Build Classes

1. Change your base class to inherit from `Base<TParams, TNotifications>`
2. Use the `Git`, `GitHub`, `Docker`, `Node`, and `NotificationService` properties
3. Override `ConfigureServices` if you need custom services

## Advanced Configuration

### Environment-Specific Configuration

Services can be configured differently based on environment:

```csharp
services.AddForgeServices(options =>
{
    options.GitHubApiUrl = Environment.GetEnvironmentVariable("GITHUB_API_URL") ?? "https://api.github.com";
    options.DockerRegistry = Environment.GetEnvironmentVariable("DOCKER_REGISTRY") ?? "ghcr.io";
});
```

### Custom Service Implementation

```csharp
public class CustomGitService : IGitService
{
    private readonly ILogger<CustomGitService> _logger;
    
    public CustomGitService(ILogger<CustomGitService> logger)
    {
        _logger = logger;
    }
    
    public async Task<string> GetCurrentBranchAsync()
    {
        _logger.LogInformation("Getting current Git branch");
        // Custom implementation
        return "main";
    }
    
    // Implement other interface methods...
}
```

## Best Practices

### 1. Interface Segregation

Keep interfaces focused and small:

```csharp
// Good - focused interface
public interface IGitTagService
{
    Task<bool> CreateTagAsync(string tagName, string message);
    Task<bool> PushTagAsync(string tagName);
}

// Bad - too broad
public interface IGitEverythingService
{
    // Too many responsibilities
}
```

### 2. Dependency Injection Guidelines

- **Use Constructor Injection in Custom Services**: Follow standard DI patterns
- **Register Services at Startup**: Configure all services during container setup
- **Use Interfaces**: Always depend on abstractions, not concrete types
- **Dispose Properly**: The base class handles disposal, but be mindful in custom code
- **Avoid Service Locator in New Code**: Prefer constructor injection over service locator
- **Test with Mocks**: Use the DI system to inject mocks during testing

### 3. Service Implementation

```csharp
public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;
    
    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }
    
    public async Task<string> GetCurrentBranchAsync()
    {
        _logger.LogInformation("Getting current Git branch");
        // Implementation
        return await Task.FromResult("main");
    }
}
```

### 4. Error Handling

Services should handle errors gracefully and provide meaningful logging:

```csharp
public async Task<bool> CreateReleaseAsync(string tagName, string releaseName, string body)
{
    try
    {
        _logger.LogInformation($"Creating GitHub release: {releaseName}");
        // Implementation
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Failed to create GitHub release: {releaseName}");
        return false;
    }
}
```

## Troubleshooting

### Common Issues

1. **Service Not Registered**: Ensure you've called `AddForgeServices()` or registered the service manually
2. **Generic Constraints**: Notification services must be reference types with parameterless constructors
3. **Disposal Issues**: Services are automatically disposed when the container is disposed
4. **Static Context**: Use `ServiceLocator` for accessing services in static methods

### Error Messages

- `"Service of type X is not registered"`: Add the service to the container using `services.AddSingleton<T>()`
- `"ServiceLocator is not initialized"`: Call `ServiceLocator.Initialize()` or `ServiceLocator.InitializeWithDefaultServices<T>()`
- `"ServiceLocator is already initialized"`: Only initialize once, or call `ServiceLocator.Reset()` first

### Debug Tips

1. **Enable Verbose Logging**: Set logging level to Debug to see service resolution
2. **Check Service Registration**: Verify services are registered in the correct order
3. **Validate Dependencies**: Ensure all service dependencies are also registered
4. **Use Container Validation**: Call `serviceProvider.GetRequiredService<T>()` to test registration

## Migration from Legacy Code

### Step 1: Identify External Dependencies

Find code that directly calls external tools or APIs:

```csharp
// Legacy code
Process.Start("git", "tag v1.0.0");
Process.Start("docker", "build -t myapp .");
```

### Step 2: Create Service Interfaces

Define interfaces for these operations:

```csharp
public interface IGitService
{
    Task<bool> CreateTagAsync(string tagName);
}
```

### Step 3: Implement Services

Create implementations that handle the actual work:

```csharp
public class GitService : IGitService
{
    public async Task<bool> CreateTagAsync(string tagName)
    {
        // Implementation using Process.Start or LibGit2Sharp
        return true;
    }
}
```

### Step 4: Update Build Classes

Inject services into build classes and use them instead of direct calls:

```csharp
public class MyBuild : Base<MyParams, MyNotifications>
{
    // Use this.Git instead of Process.Start("git", ...)
    public Target CreateTag => _ => _
        .Executes(async () =>
        {
            await Git.CreateTagAsync("v1.0.0");
        });
}
```

This dependency injection architecture provides a solid foundation for testable, maintainable build processes while maintaining backward compatibility with existing code through the service locator pattern.
