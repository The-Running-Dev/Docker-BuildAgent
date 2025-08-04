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
  - Make sure you specify the correct `-type` argument (e.g., `docker`, `node`, `forge`).
- **Changelog generation issues:**
  - Ensure your Git repository has commit history and proper tag structure.
  - Check that the repository has at least one tag if using the default (since last tag) option.
  - Verify write permissions to the project directory for CHANGELOG.md creation.
- **Date formatting problems:**
  - The changelog formatter uses `yyyy.MM.dd` format by default; custom formats require code changes.
  - Ensure commit dates are properly parsed from Git history.

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
  - A: Use the `-type` argument with the build script, e.g., `./build.ps1 -type docker`, `./build.ps1 -type node`, or `./build.ps1 -type forge`.
- **Q: Why is my changelog empty or not generating correctly?**
  - A: Ensure your Git repository has commits and tags. Use `--change-log-source all` to generate complete history, or verify the last tag exists with `git tag -l`.
- **Q: Can I customize the changelog date format?**
  - A: The default format is `yyyy.MM.dd`. Customization requires modifying the `ChangeLogFormatOptions` class in the Forge source code.
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
