## Complete History (Generated 2025-07-27)

### 2025-07-22

- Adding Missing Binaries
- Update gitignore, build schema, Updated Dockerfile setup, build script, and documentation scripts. Refactor DockerParams class for managing Docker images.

### 2025-07-20

- Remove subproject from docs-ui
- Update repository_dispatch event type for documentation workflow
- Additional Docs, Better Styling, Logo
- Update default value for isProduction parameter
- Build and Docs Fixes
- Update CI and release workflows to ignore '.github/**' paths.
- Update workflow to set default shell to PowerShell.
- Merge pull request #4 from The-Running-Dev/feature/cleanup
- Merge remote-tracking branch 'origin/main' into feature/cleanup
- Update CI, Docs, and Release workflows triggers and conditions.

### 2025-07-19

- Merge pull request #3 from The-Running-Dev/feature/cleanup
- Update condition to skip push events for open PRs on main branch
- Update CI workflow to skip job runs for specific paths and PRs
- Update .NET SDK version output and add label for pull request branches
- Update CI and Release workflows to ignore changes in 'documentation' directory.
- Update build command in workflow to include app directory for documentation.
- Update composite action setup and workflows for Docker/Nuke builds.
- Workflow Cleanup
- Refactor workflow to use common action setup - Renamed and updated workflow files to utilize common action setup in a shared directory.
- Update build process for documentation, add new scripts and modules.
- Refactor: Renamed documentation files and directories.
- Restructure and Cleanup
- Cleanup

### 2025-07-18

- Updated README
- Temporary Rename
- Update build script to use docker type for Forge.dll execution.
- Update path for artifact upload and streamline build script execution
- CI Fixes
- Update environment variables and secrets handling
- Update environment variables and workflow steps for GitHub actions
- Update cache setting to npm, add new build script
- Add pnpm installation, fixed failing build
- Merge pull request #2 from The-Running-Dev/feature/next_version
- Update Forge/Common/Utilities/Files.cs
- Update Forge/Forge.csproj.DotSettings
- Update Forge/Common/Utilities/Git.cs
- Update Forge/Common/Utilities/Node.cs
- Update Forge/Common/Base.cs
- Update Forge/Common/Entities/CommitInfo.cs
- Update Forge/Common/Entities/BuildConfig.cs
- Update Forge/Node/Properties/launchSettings.json
- Cleanup and Fixes

### 2025-07-17

- Working Next Version

### 2025-07-09

- Added Build Notifications

### 2025-06-04

- Forces tag creation and push

### 2025-06-02

- Fixes typo in workflow
- Updates documentation and adds ContainerCI details
- Ensures consistent Git tag creation and pushing
- Adds version prefix to git tags
- dummy commit

### 2025-06-01

- Renames CI workflow for container builds
- Simplifies CI workflow and GitVersion config.
- Adds git safe directory configuration
- Improves Docker CI script robustness.
- Renames container CI command
- Renames GitHubPackagesToken to RepositoryToken
- Refactors build process to use Docker project

### 2025-05-31

- Refactors build pipeline dependencies
- Removes Publish target from DockerPipeline
- Fixes target name in CI workflow
- Adds GitVersion target and force CI behavior
- Refactors CI pipeline for Docker builds
- Grants workflow write permissions
- Automates versioning and deployment process
- Removes unnecessary nuke plan execution.
- Adds GetVersion target to CI and requires version file
- Updates NUKE build verbosity
- Adds verbosity to the build process
- Improves build process and configuration
- Removes redundant GitVersion invocation
- Uses `dotnet-gitversion` instead of `gitversion`
- Adds debug output for tool installation
- Updates .NET tool installation path
- Streamlines .NET tool installation in CI
- Makes gitversion accessible in path
- Migrates to NUKE build system

### 2025-05-30

- Adds .NET 8 SDK, Git, and GitVersion to build agent

### 2025-05-26

- Simplifies build workflow description
- Removes trivy scan and unused dependency
- Adds deployment dependencies
- Removes unused npm packages
- Reorders npm install packages
- Updates workflow trigger path
- Adds gh-pages package
- Updates angular-cli-ghpages to the latest version
- Removes Dockerfile linting from CI
- Updates Trivy action version
- Updates Trivy action version
- Updates Trivy action to latest version
- Updates shellcheck action
- Enhances build process with linting, tagging, and security
- Adds build and push workflow for the agent
- First Commit


## Complete History (Generated 2025-07-27)

### 2025-07-22

- Adding Missing Binaries
- Update gitignore, build schema, Updated Dockerfile setup, build script, and documentation scripts. Refactor DockerParams class for managing Docker images.

### 2025-07-20

- Remove subproject from docs-ui
- Update repository_dispatch event type for documentation workflow
- Additional Docs, Better Styling, Logo
- Update default value for isProduction parameter
- Build and Docs Fixes
- Update CI and release workflows to ignore '.github/**' paths.
- Update workflow to set default shell to PowerShell.
- Merge pull request #4 from The-Running-Dev/feature/cleanup
- Merge remote-tracking branch 'origin/main' into feature/cleanup
- Update CI, Docs, and Release workflows triggers and conditions.

### 2025-07-19

- Merge pull request #3 from The-Running-Dev/feature/cleanup
- Update condition to skip push events for open PRs on main branch
- Update CI workflow to skip job runs for specific paths and PRs
- Update .NET SDK version output and add label for pull request branches
- Update CI and Release workflows to ignore changes in 'documentation' directory.
- Update build command in workflow to include app directory for documentation.
- Update composite action setup and workflows for Docker/Nuke builds.
- Workflow Cleanup
- Refactor workflow to use common action setup - Renamed and updated workflow files to utilize common action setup in a shared directory.
- Update build process for documentation, add new scripts and modules.
- Refactor: Renamed documentation files and directories.
- Restructure and Cleanup
- Cleanup

### 2025-07-18

- Updated README
- Temporary Rename
- Update build script to use docker type for Forge.dll execution.
- Update path for artifact upload and streamline build script execution
- CI Fixes
- Update environment variables and secrets handling
- Update environment variables and workflow steps for GitHub actions
- Update cache setting to npm, add new build script
- Add pnpm installation, fixed failing build
- Merge pull request #2 from The-Running-Dev/feature/next_version
- Update Forge/Common/Utilities/Files.cs
- Update Forge/Forge.csproj.DotSettings
- Update Forge/Common/Utilities/Git.cs
- Update Forge/Common/Utilities/Node.cs
- Update Forge/Common/Base.cs
- Update Forge/Common/Entities/CommitInfo.cs
- Update Forge/Common/Entities/BuildConfig.cs
- Update Forge/Node/Properties/launchSettings.json
- Cleanup and Fixes

### 2025-07-17

- Working Next Version

### 2025-07-09

- Added Build Notifications

### 2025-06-04

- Forces tag creation and push

### 2025-06-02

- Fixes typo in workflow
- Updates documentation and adds ContainerCI details
- Ensures consistent Git tag creation and pushing
- Adds version prefix to git tags
- dummy commit

### 2025-06-01

- Renames CI workflow for container builds
- Simplifies CI workflow and GitVersion config.
- Adds git safe directory configuration
- Improves Docker CI script robustness.
- Renames container CI command
- Renames GitHubPackagesToken to RepositoryToken
- Refactors build process to use Docker project

### 2025-05-31

- Refactors build pipeline dependencies
- Removes Publish target from DockerPipeline
- Fixes target name in CI workflow
- Adds GitVersion target and force CI behavior
- Refactors CI pipeline for Docker builds
- Grants workflow write permissions
- Automates versioning and deployment process
- Removes unnecessary nuke plan execution.
- Adds GetVersion target to CI and requires version file
- Updates NUKE build verbosity
- Adds verbosity to the build process
- Improves build process and configuration
- Removes redundant GitVersion invocation
- Uses `dotnet-gitversion` instead of `gitversion`
- Adds debug output for tool installation
- Updates .NET tool installation path
- Streamlines .NET tool installation in CI
- Makes gitversion accessible in path
- Migrates to NUKE build system

### 2025-05-30

- Adds .NET 8 SDK, Git, and GitVersion to build agent

### 2025-05-26

- Simplifies build workflow description
- Removes trivy scan and unused dependency
- Adds deployment dependencies
- Removes unused npm packages
- Reorders npm install packages
- Updates workflow trigger path
- Adds gh-pages package
- Updates angular-cli-ghpages to the latest version
- Removes Dockerfile linting from CI
- Updates Trivy action version
- Updates Trivy action version
- Updates Trivy action to latest version
- Updates shellcheck action
- Enhances build process with linting, tagging, and security
- Adds build and push workflow for the agent
- First Commit


## Changes Since 0.7.0 (Generated 2025-07-27)

### 2025-07-22

- Adding Missing Binaries
- Update gitignore, build schema, Updated Dockerfile setup, build script, and documentation scripts. Refactor DockerParams class for managing Docker images.

