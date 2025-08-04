---
id: multi-build-architecture
title: 🏗️ Multi-Build Architecture
sidebar_position: 1
---

The Docker Build Agent solution supports **multiple independent builds** within the same solution structure, enabling specialized build processes for different project types and use cases.

## Build Types Available

1. **Docker Build** (`forge/Docker/`) - Container builds and deployment
2. **Node Build** (`forge/Node/`) - Node.js applications and documentation
3. **NodeInDocker Build** (`forge/NodeInDocker/`) - Combined Node.js + Docker pipeline
4. **NodeTemplate Build** (`forge/NodeTemplate/`) - Template-based documentation sites
5. **Forge Build** (`forge/Forge/`) - Build orchestration and tooling

## Build Architecture

```text
Forge.sln
├── Common/              (shared utilities - referenced by all builds)
├── Docker/              (independent Docker build)
│   ├── Docker.cs        (main build class)
│   ├── Docker.csproj    (executable project)
│   └── Parameters/      (Docker-specific parameters)
├── Node/                (independent Node.js build)
│   ├── Node.cs          (main build class)
│   ├── Node.csproj      (executable project)
│   └── Parameters/      (Node-specific parameters)
├── NodeInDocker/        (combined Node.js + Docker build)
│   ├── NodeInDocker.cs  (main build class)
│   ├── NodeInDocker.csproj (executable project)
│   └── Parameters/      (NodeInDocker-specific parameters)
├── NodeTemplate/        (template-based documentation build)
│   ├── NodeTemplate.cs  (main build class)
│   ├── NodeTemplate.csproj (executable project)
│   └── Parameters/      (NodeTemplate-specific parameters)
├── Forge/               (independent orchestrator build)
│   ├── Forge.cs         (main build class)
│   ├── Forge.csproj     (executable project)
│   └── Parameters/      (Forge-specific parameters)
└── [Build Scripts at Root Level]
```

## Usage Options

### Option 1: Container-Based (Recommended)

Use the Docker Build Agent container to execute builds:

```bash
# Docker build
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  docker-build

# Node.js build
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  node-build

# Combined Node.js + Docker build
docker run \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  node-in-docker-build

# Template-based documentation build
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  node-template-build

# Changelog generation and build orchestration
docker run \
  -v ./:/workspace \
  -it ghcr.io/the-running-dev/build-agent:latest \
  forge --target GenerateChangeLog
```

### Option 2: Local PowerShell Scripts

Use the local build script for development:

```powershell
# Basic usage
.\build.ps1 -type docker
.\build.ps1 -type node
.\build.ps1 -type node-in-docker
.\build.ps1 -type forge

# With additional parameters
.\build.ps1 -type docker -isProd --image-tag myapp:v1.0.0
.\build.ps1 -type node --artifacts-dir ./dist
.\build.ps1 -type forge --change-log-source all
```

### Option 3: Direct NUKE Commands

Run builds directly with NUKE CLI (for development):

```powershell
# Compile and run specific projects
dotnet run --project forge/Docker/Docker.csproj -- Build
dotnet run --project forge/Node/Node.csproj -- Build
dotnet run --project forge/Forge/Forge.csproj -- GenerateChangeLog
dotnet run --project forge/Node/Node.csproj -- Build
dotnet run --project forge/NodeInDocker/NodeInDocker.csproj -- Build
dotnet run --project forge/NodeTemplate/NodeTemplate.csproj -- Build

# Or use NUKE CLI after building
nuke --root forge/Docker Build
nuke --root forge/Node Build
```

## Build Responsibilities

### Docker Build (`forge/Docker/`)

- **Purpose**: Container image creation, registry management, deployment
- **Key Targets**: `Setup`, `Clean`, `Build`, `BuildDockerImage`, `PushToRegistry`, `PublishToGitHub`
- **Parameters**: Registry URLs, image tags, Dockerfiles, authentication tokens
- **Dependencies**: Docker Engine, container registries
- **Use Cases**: Containerizing existing applications, infrastructure deployment

### Node Build (`forge/Node/`)

- **Purpose**: Node.js application builds, package management, artifact creation
- **Key Targets**: `Setup`, `Clean`, `InstallDependencies`, `BuildApplication`, `CopyToArtifacts`
- **Parameters**: Package managers, build scripts, output directories
- **Dependencies**: Node.js, npm/yarn/pnpm
- **Use Cases**: Frontend applications, Node.js APIs, static site generation

### NodeInDocker Build (`forge/NodeInDocker/`)

- **Purpose**: Complete CI/CD pipeline combining Node.js build with Docker containerization
- **Key Targets**: 10-step pipeline from setup to GitHub release
- **Parameters**: Combines all Node and Docker parameters
- **Dependencies**: Node.js, Docker Engine, container registries
- **Use Cases**: Production deployments, microservices, containerized web applications

### NodeTemplate Build (`forge/NodeTemplate/`)

- **Purpose**: Documentation site generation using templates (Docusaurus, etc.)
- **Key Targets**: `Setup`, `CloneTemplate`, `CopyTemplateFiles`, `BuildDocumentation`
- **Parameters**: Template repositories, documentation directories, build configurations
- **Dependencies**: Node.js, Git, template repositories
- **Use Cases**: Project documentation, API documentation, static documentation sites

### Forge Build (`forge/Forge/`)

- **Purpose**: Build orchestration, changelog generation, release management
- **Key Targets**: `ChangeLog`, `Build`, `Setup`, `Release`
- **Parameters**: Git tags, changelog sources, release configurations
- **Dependencies**: Git, build tools, notification systems
- **Use Cases**: Release management, build coordination, changelog automation

## Shared Components

### Common Project (`forge/Common/`)

All builds share utilities from the Common project:

- **Services/**: Git, GitHub, Docker, and Node.js service interfaces
- **Utilities/**: Helper classes and extension methods
- **Extensions/**: File system and process extensions
- **Parameters/**: Base parameter classes with inheritance hierarchy
- **Notifications/**: Discord and other notification systems
- **DependencyInjection/**: Service container and dependency management

### Base Classes

All builds inherit from `Base<TParams, TNotifications>`:

- Provides consistent logging with Serilog
- Standardized parameter handling with dependency injection
- Built-in notification support
- Common target patterns and error handling
- Service injection for Git, GitHub, Docker, and Node.js operations

### Parameter Inheritance

The parameter system uses inheritance for shared functionality:

```text
ForgeParams (base)
├── DockerParams (extends ForgeParams)
├── NodeParams (extends ForgeParams)
└── NodeInDockerParams (extends DockerParams)
```

This allows builds to share common parameters while maintaining their specific configurations.

## Adding New Build Types

To add another build type (e.g., "Database", "Mobile", "API"):

### 1. Create Project Structure

```text
forge/Database/
├── Database.cs           (main build class)
├── Database.csproj       (executable project)
├── Parameters/
│   └── DatabaseParams.cs (parameter class)
└── Services/
    └── IDatabaseService.cs (service interface)
```

### 2. Implement Build Class

```csharp
public class Database : Base<DatabaseParams, DiscordNotifications>
{
    public Target Setup => _ => _
        .Executes(() => {
            // Build setup logic
        });

    public Target Build => _ => _
        .DependsOn(Setup)
        .Executes(() => {
            // Main build logic
        });
}
```

### 3. Add to Container

Add the new executable to the Docker Build Agent container in the Dockerfile.

### 4. Update Documentation

Add the new build type to the build-types.md documentation with usage examples and parameters.

## Best Practices

### 1. Build Independence

- Each build should be runnable independently
- Minimize cross-dependencies between builds
- Use Common project for shared functionality only
- Implement proper error handling and cleanup

### 2. Consistent Patterns

- Follow the same `Base<TParams, TNotifications>` inheritance
- Use consistent target naming conventions (Setup, Clean, Build)
- Implement dependency injection for services
- Use strongly-typed parameters with validation

### 3. Clear Responsibilities

- Keep each build focused on its specific domain
- Avoid feature overlap between builds
- Document build purposes and capabilities
- Provide clear usage examples and parameter documentation

### 4. Service Management

- Use dependency injection for all external dependencies
- Implement service interfaces for testability
- Register services in the Common project
- Follow SOLID principles for service design

## Migration Strategy

### From Single Build to Multi-Build

1. **Gradual Migration**: Start with one specialized build type
2. **Maintain Compatibility**: Keep existing scripts working during transition
3. **Test Independently**: Each build can be developed and tested separately
4. **Documentation**: Update docs as new build types are added

### Legacy Support

- Original `build.ps1` continues to work for Docker builds
- Container-based execution provides consistent environment
- PowerShell scripts offer local development experience
- Direct NUKE commands available for advanced users

## Troubleshooting

### Common Issues

#### Build Not Found

```text
No build type specified or invalid build type
```

**Solution**: Use valid build types: docker, node, node-in-docker, node-template

#### Project Not Found

```text
Could not find project 'forge/[BuildType]/[BuildType].csproj'
```

**Solution**: Ensure the project exists and is built (`dotnet build`)

#### Parameter Issues

```text
Unknown parameter '--some-param'
```

**Solution**: Check parameter documentation for the specific build type

#### Service Dependencies

```text
Service not registered or dependency injection failure
```

**Solution**: Verify services are registered in ServiceCollectionExtensions

### Debug Tips

1. Use `--verbosity Verbose` for detailed logging
2. Use `--dry-run true` to test without side effects
3. Check container logs for Docker-related issues
4. Verify environment variables and secrets are set correctly
