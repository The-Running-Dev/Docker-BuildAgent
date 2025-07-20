---
id: troubleshooting
title: "❓ Troubleshooting & FAQ"
sidebar_position: 11
---

This section covers common issues, troubleshooting steps, and frequently asked questions for Docker-BuildAgent and the Forge build orchestrator.

## Troubleshooting

- **Docker login/authentication errors:**
  - Ensure your `RegistryToken` is valid and has the correct permissions (`write:packages`).
  - Use GitHub Actions secrets for sensitive values.
- **Image push fails:**
  - Check your network connection and GHCR access rights.
- **.NET build issues:**
  - Ensure you have the .NET 8 SDK installed locally if running .NET builds outside the container.
- **CI tool access issues:**
  - Ensure the CI environment has access to the required tools and permissions. The workflow sets up tools in the `/root/.dotnet/tools` directory and updates the PATH.
- **Forge build type errors:**
  - Make sure you specify the correct `-type` argument (e.g., `docker`, `node`).

## FAQ

- **Q: I get a permission denied error on build.sh or build.ps1**
  - A: Run `chmod +x build.sh` (Linux/macOS) or ensure PowerShell script permissions (Windows).
- **Q: .NET build fails outside the container**
  - A: Make sure you have the .NET 8 SDK installed locally.
- **Q: How do I pass secrets to Forge, Nuke, or Docker builds?**
  - A: Use environment variables or GitHub Actions secrets. Never hardcode secrets in scripts or Dockerfiles.
- **Q: GitVersion or Nuke not found in CI?**
  - A: The workflow installs .NET tools globally, creates a symlink for GitVersion, and adds `/root/.dotnet/tools` to the PATH for reliable access.
- **Q: How do I run a build for a specific project type?**
  - A: Use the `-type` argument with the build script, e.g., `./build.ps1 -type docker` or `./build.ps1 -type node`.
- **Q: What happens when you run `ContainerCI` or the `container-ci` command?**
  - A: The `ContainerCI` target runs the full build, versioning, tagging, and publishing pipeline in order:
    1. Clean
    2. GetVersion
    3. ValidateInputs
    4. PrintInfo
    5. BuildContainer
    6. Tag
    7. Push
    8. Publish
    9. ContainerCI

Each target depends on the previous one, ensuring the full pipeline is executed correctly.

For more help, see the project README or open an issue on GitHub.
