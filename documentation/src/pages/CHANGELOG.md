## History (2026.01.29)

### 2025.08.06

- Updated Docs Config
- Add GitHub Copilot instructions for Docker-BuildAgent, including project overview, core architecture with specialized build types, base class pattern, component interfaces, build configuration system with files structure and syntax examples, key components/services, parameter inheritance hierarchy, utilities description. Also includes build execution patterns with container-based execution and local PowerShell scripts examples. Lastly covers PowerShell automation core helper module functions and build script architecture details.

### 2025.08.04

- Add environment variables for GitHub and Registry tokens, and Notifications WebHook URL.
- Add setup steps for environment and fetch latest changelog.
- Add latest changelog retrieval step in documentation workflow
- Update workflow name to "Release" for clarity and consistency.
- Update workflow job names and step descriptions
- The Final Countdown
- Update gitignore to include new directories and remove navbarLinks.ts. Refactor documentation links for consistency and fix typos in fast-track.md and parameters.md.
- Fixing Docs Build Workflow
- Fixing Markdown
- Feature/wip (#10)
- Add changelog generation and build orchestration options for Forge. Update examples in documentation to reflect new features and capabilities.
- Fixes for the change log generator
- Update build script to include 'forge' as a build type option. Refactor NodeService to handle different shell commands for package managers and shells.
- Update NodeService to include a force flag in package manager install, improve logging for warnings and errors. Update Docker, Node, and NodeInDocker classes to use 'Ok' log level for build completion message.
- Update documentation directory path and Docker build configuration for template build phase. Standardize parameter names in the script for consistency.
- Refactor build classes to use shared components for deduplication.
- Cleanup, Updated Docs Template
- Merge branch 'main' into feature/wip
- Refactor git clone to support branch specification (#9)
- Update branch handling in node template build script
- Addressed PR issues
- Refactor git clone to support branch specification
- WIP
- Clean up in favor of new template

### 2025.08.03

- Work in Progress

### 2025.07.29

- Build Fix and Docs Update
- Merge pull request #7 from The-Running-Dev/feature/code_coverage_in_ci
- Update CI and release workflows with enhanced testing, coverage reporting, and documentation integration.
- Delete unnecessary build log file and update logger warning message in NodeServiceTests.
- Update CI and release workflows to execute test steps. Refactor logger extensions for error and warning messages. Update GitService, NodeService, Docker, and NodeInDocker classes to log errors with exceptions.
- Add CI workflow for running tests with coverage and reporting
- Add write permissions for checks and pull requests, update job names. Improve readability by removing emojis from job names.
- Update test results directory and add actions for publishing test results, generating coverage report, adding coverage summary to PR, and uploading detailed coverage report as artifact.
- Add test coverage collection and report upload, validate build process.
- Merge pull request #6 from The-Running-Dev/hotfix/fixing_build_warnings
- Refactor Docker and NodeInDocker classes for GitHub release and Git tag creation
- Fixing Build Warnings
- Merge pull request #5 from The-Running-Dev/hotfix/tags_and_releases
- Add workflow action to skip duplicate runs
- Cleanup to Address PR Review
- Update CI, Docs, and Release workflows
- Update build script to handle dotnet installation and execution
- Refactor build scripts, remove redundant shell settings and emojis.
- Update workflows to set working directory for build and release jobs.
- Update CI and release workflows to use 'nuke' commands for Docker operations.
- - Automated testing for Docker Build Agent project, PRs & branches. - Optimized CI resource usage - Added duplicate action prevention - Enhanced build process validation - Implemented new services and DI usages - Added new NodeInDocker build type - Added more tests - Updated documentation - Cleanup

### 2025.07.22

- Adding Missing Binaries
- Update gitignore, build schema, Updated Dockerfile setup, build script, and documentation scripts. Refactor DockerParams class for managing Docker images.

### 2025.07.20

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

### 2025.07.19

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

### 2025.07.18

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

### 2025.07.17

- Working Next Version

### 2025.07.09

- Added Build Notifications

### 2025.06.04

- Forces tag creation and push

### 2025.06.02

- Fixes typo in workflow
- Updates documentation and adds ContainerCI details
- Ensures consistent Git tag creation and pushing
- Adds version prefix to git tags
- dummy commit

### 2025.06.01

- Renames CI workflow for container builds
- Simplifies CI workflow and GitVersion config.
- Adds git safe directory configuration
- Improves Docker CI script robustness.
- Renames container CI command
- Renames GitHubPackagesToken to RepositoryToken
- Refactors build process to use Docker project

### 2025.05.31

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

### 2025.05.30

- Adds .NET 8 SDK, Git, and GitVersion to build agent

### 2025.05.26

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

