---
id: development-guide
title: 🛠️ Development Guide
sidebar_position: 3
---

Complete guide for setting up, developing, and contributing to the Docker Build Agent project.

## Prerequisites

### Required Software

- **Docker**: [Docker Desktop](https://www.docker.com/get-started) installed and running (for local builds)
- **.NET 8 SDK**: [Download .NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (for local .NET builds)
- **PowerShell**: PowerShell 5.1+ (Windows) or PowerShell Core 7+ (Cross-platform)
- **Git**: Git client for version control
- **Node.js**: Node.js 18+ (for documentation and Node.js builds)

### Optional Development Tools

- **Visual Studio 2022** or **Visual Studio Code** with C# extension
- **JetBrains Rider** for .NET development
- **GitHub CLI** for enhanced GitHub integration

### Access Requirements

- **GitHub Account**: For repository access and container registry
- **GitHub Container Registry (GHCR)**: Access to ghcr.io
- **Personal Access Token**: GitHub token with packages:write permissions to push to GHCR

## Environment Setup

### 1. Clone the Repository

```bash
git clone https://github.com/The-Running-Dev/Docker-BuildAgent.git
cd Docker-BuildAgent
```

### 2. Build the Docker Image (Quick Start)

For immediate testing, you can build the container image:

```bash
# Build the build-agent container
docker build -t build-agent:latest .

# Test the container
docker run -it build-agent:latest
```

### 3. Set Environment Variables

Create a `.env` file in the project root (this file is gitignored):

```bash
# GitHub Configuration
GITHUB_TOKEN=your_github_personal_access_token
REGISTRY_TOKEN=your_github_personal_access_token
GITHUB_ACTOR=your_github_username

# Registry Configuration
REGISTRY_URL=ghcr.io
REGISTRY_USER=your_github_username

# Optional: Discord Notifications
NOTIFICATIONS_WEBHOOK_URL=your_discord_webhook_url

# Optional: Development Settings
VERBOSITY=Verbose
DRY_RUN=false
```

### 4. Build the Solution

```bash
# Build all projects
dotnet build forge/Forge.sln

# Or build specific projects
dotnet build forge/Docker/Docker.csproj
dotnet build forge/Node/Node.csproj
dotnet build forge/NodeInDocker/NodeInDocker.csproj
```

### 5. Run Tests

```bash
# Run all tests
dotnet test forge/Forge.sln

# Run specific test project
dotnet test forge/Common.Tests/Common.Tests.csproj
```

## Development Workflow

### Local Development

#### Using the Local Build Script

```powershell
# Basic Docker build
.\build.ps1 -type docker

# Node.js build with production flag
.\build.ps1 -type node -isProd

# Combined Node + Docker build with parameters
.\build.ps1 -type node-in-docker --dry-run true --verbosity Verbose
```

#### Direct Project Execution

```bash
# Run Docker build directly
dotnet run --project forge/Docker/Docker.csproj -- Build --dry-run true

# Run Node build with specific parameters
dotnet run --project forge/Node/Node.csproj -- Build --artifacts-dir ./dist

# Run NodeInDocker build
dotnet run --project forge/NodeInDocker/NodeInDocker.csproj -- Build --create-github-release false
```

### Container-Based Development

#### Building the Build Agent Container

```bash
# Build the container image
docker build -t build-agent:dev .

# Build with specific tag
docker build -t build-agent:latest .
```

#### Testing Container Locally

```bash
# Test Docker build
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -e GITHUB_TOKEN=$GITHUB_TOKEN \
  -it build-agent:dev \
  docker-build --dry-run true

# Test Node build
docker run \
  -v ./:/workspace \
  -it build-agent:dev \
  node-build --artifacts-dir ./test-output
```

## 📁 Project Structure

The project follows a modular architecture with shared components:

```text
Docker-BuildAgent/
├── Common/                     # Shared utilities and interfaces
│   ├── Interfaces/            # Service interfaces (IGitService, IDockerService, etc.)
│   ├── Services/              # Service implementations
│   └── Models/                # Data models and DTOs
├── Docker/                    # Docker build type implementation
├── Node/                      # Node.js build type implementation
├── forge/                     # Forge multi-project builds
│   ├── NodeInDocker/         # Node.js in Docker container
│   └── NodeTemplate/         # Node.js template project
├── templates/                 # Project templates
├── scripts/                   # Build and deployment scripts
├── artifacts/                 # Build output directory
└── documentation/             # Docusaurus documentation site
```

### Core Components

- **Common Project**: Contains shared services, interfaces, and utilities used across all build types
- **Build Types**: Independent implementations (Docker, Node) with specific functionality
- **Forge Projects**: Multi-project builds that combine functionality from multiple build types
- **Templates**: Reusable project templates for quick project initialization

### Solution Organization

```text
Docker-BuildAgent/
├── forge/                      # Main solution directory
│   ├── Forge.sln              # Main solution file
│   ├── Common/                # Shared utilities and services
│   │   ├── Services/          # Service interfaces and implementations
│   │   ├── Parameters/        # Base parameter classes
│   │   ├── DependencyInjection/  # DI container setup
│   │   └── Extensions/        # Extension methods
│   ├── Docker/                # Docker build project
│   ├── Node/                  # Node.js build project
│   ├── NodeInDocker/          # Combined Node+Docker build project
│   ├── NodeTemplate/          # Template-based documentation build
│   └── Common.Tests/          # Unit tests
├── scripts/                   # PowerShell build scripts
├── documentation/             # Docusaurus documentation site
├── templates/                 # Dockerfile templates
├── .github/                   # GitHub Actions workflows
├── build.ps1                  # Main build script
└── Dockerfile                 # Build agent container definition
```

### Key Files

- **`build.ps1`**: Main entry point for local builds
- **`Dockerfile`**: Defines the build agent container
- **`forge/Forge.sln`**: Main .NET solution
- **`scripts/nuke/nuke-helpers.psm1`**: PowerShell helper functions
- **`.github/workflows/`**: CI/CD pipeline definitions

### Build Configuration Files

The project uses several configuration files to control build behavior:

```text
/.build
├── .app.env.map         # Maps application env vars
├── .build.scripts       # List of commands (e.g. npm, ps1, bash)
├── .build.copy          # Files/folders to copy to artifacts/
├── .build.env.map       # Maps build env vars like DiscordWebHookUrl
/artifacts/              # Final build output ends up here
/documentation/          # Docusaurus documentation
/forge/                  # Shared NUKE build logic
├──/Docker/              # Docker-specific targets
├──/Node/                # Node.js-specific targets
./Dockerfile             # Containerize your build
./build.ps1              # Build entry point
```

## 🔧 Forge Multi-Project Builds

The Forge solution provides a unified build system that combines multiple build types into specialized implementations:

### Available Forge Projects

1. **NodeInDocker**: Combines Node.js and Docker build capabilities
2. **NodeTemplate**: Template-based project generation with Node.js support

### Building Forge Projects

```bash
# Build all Forge projects
dotnet build forge/Forge.sln

# Build specific project
dotnet build forge/NodeInDocker/NodeInDocker.csproj
```

## Adding New Features

### 1. Creating a New Build Type

Follow the multi-build architecture to add new build types:

```csharp
// 1. Create new project directory
forge/MyNewBuild/
├── MyNewBuild.cs
├── MyNewBuild.csproj
└── Parameters/
    └── MyNewBuildParams.cs

// 2. Implement the build class
public class MyNewBuild : Base<MyNewBuildParams, DiscordNotifications>
{
    public Target Setup => _ => _
        .Executes(() => {
            Logger.Information("Setting up MyNewBuild");
        });

    public Target Build => _ => _
        .DependsOn(Setup)
        .Executes(() => {
            Logger.Information("Executing MyNewBuild");
        });
}

// 3. Add to solution
dotnet sln forge/Forge.sln add forge/MyNewBuild/MyNewBuild.csproj

// 4. Update Dockerfile to include new executable
```

### 2. Adding New Services

Extend the dependency injection system:

```csharp
// 1. Define service interface
public interface IMyNewService
{
    Task<bool> DoSomethingAsync(string parameter);
}

// 2. Implement service
public class MyNewService : IMyNewService
{
    private readonly ILogger<MyNewService> _logger;
    
    public MyNewService(ILogger<MyNewService> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> DoSomethingAsync(string parameter)
    {
        _logger.LogInformation($"Doing something with {parameter}");
        // Implementation
        return true;
    }
}

// 3. Register in ServiceCollectionExtensions
services.AddTransient<IMyNewService, MyNewService>();

// 4. Use in build classes
public class MyBuild : Base<MyParams, MyNotifications>
{
    private readonly IMyNewService _myNewService;
    
    public MyBuild()
    {
        _myNewService = ServiceLocator.GetRequiredService<IMyNewService>();
    }
}
```

### 3. Adding New Parameters

Extend the parameter system:

```csharp
// 1. Create parameter class
public class MyNewBuildParams : ForgeParams
{
    [Parameter("Description of my parameter")]
    public string MyParameter { get; set; } = "default-value";
    
    [Parameter("Another parameter with validation")]
    public int MyNumberParameter { get; set; } = 42;
}

// 2. Use in build class
public class MyNewBuild : Base<MyNewBuildParams, DiscordNotifications>
{
    public Target Build => _ => _
        .Executes(() => {
            Logger.Information($"Using parameter: {Parameters.MyParameter}");
        });
}
```

## Testing

### Unit Testing

```csharp
[Test]
public async Task TestBuildProcess()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddTransient<IGitService, MockGitService>();
    services.AddTransient<ILogger<MyBuild>, MockLogger<MyBuild>>();
    
    var serviceProvider = services.BuildServiceProvider();
    ServiceLocator.Initialize(serviceProvider);
    
    var build = new MyBuild();
    
    // Act
    var result = await build.ExecuteAsync();
    
    // Assert
    Assert.That(result, Is.True);
}
```

### Integration Testing

```bash
# Test with real container
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./test-project:/workspace \
  -e GITHUB_TOKEN=$GITHUB_TOKEN \
  -it build-agent:dev \
  node-in-docker-build --dry-run true
```

### Manual Testing

```bash
# Test different build types
.\build.ps1 -type docker --dry-run true
.\build.ps1 -type node --artifacts-dir ./test-output
.\build.ps1 -type node-in-docker --verbosity Verbose

# Test with different parameters
.\build.ps1 -type docker --image-tag test:latest --registry-url localhost:5000
```

## Debugging

### Local Debugging

1. **Visual Studio**: Set startup project to the build type you want to debug
2. **VS Code**: Use the provided launch configurations
3. **Command Line**: Use `dotnet run` with `--` separator for arguments

```bash
# Debug with specific arguments
dotnet run --project forge/Docker/Docker.csproj -- Build --verbosity Verbose --dry-run true
```

### Container Debugging

```bash
# Run container interactively
docker run -it --entrypoint /bin/bash build-agent:dev

# Check installed tools
which docker
which node
which dotnet

# Test build commands manually
docker-build --help
node-build --help
```

### Common Issues

#### .NET Build Errors

```bash
# Clean and rebuild
dotnet clean forge/Forge.sln
dotnet build forge/Forge.sln

# Restore packages
dotnet restore forge/Forge.sln
```

#### Docker Issues

```bash
# Check Docker daemon
docker version
docker info

# Check container logs
docker logs container-id

# Debug container
docker run -it --entrypoint /bin/bash build-agent:dev
```

#### PowerShell Module Issues

```powershell
# Reload the module
Remove-Module nuke-helpers -Force
Import-Module ./scripts/nuke/nuke-helpers.psm1 -Force

# Check module functions
Get-Command -Module nuke-helpers
```

## Contributing

### 1. Development Process

1. **Fork** the repository
2. **Create** a feature branch
3. **Make** your changes
4. **Test** thoroughly
5. **Submit** a pull request

### 2. Commit Guidelines

Follow conventional commit format:

```text
feat: add new build type for Python projects
fix: resolve Docker image tagging issue
docs: update multi-build architecture guide
test: add unit tests for GitService
```

### 3. Pull Request Process

1. **Update documentation** if needed
2. **Add tests** for new functionality
3. **Ensure all CI checks pass**
4. **Request review** from maintainers

### 4. Code Standards

- **Follow C# coding conventions**
- **Use dependency injection** for external dependencies
- **Add XML documentation** for public APIs
- **Include unit tests** for new features
- **Update relevant documentation**

## CI/CD Pipeline

### GitHub Actions Workflows

- **`ci.yml`**: Runs on pull requests and feature branches
- **`release.yml`**: Deploys to production on main branch
- **`docs.yml`**: Updates documentation site

### Local CI Testing

```bash
# Run the same commands as CI
dotnet build forge/Forge.sln
dotnet test forge/Forge.sln
docker build -t build-agent:test .
```

### Release Process

1. **Merge** to main branch
2. **Automatic** GitHub Actions build
3. **Container** pushed to GHCR
4. **GitHub release** created
5. **Documentation** updated

This development guide provides everything needed to contribute effectively to the Docker Build Agent project, from initial setup through advanced development scenarios.
