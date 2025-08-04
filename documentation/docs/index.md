---
id: index
title: 🏠 Introduction
sidebar_position: 0
---

**Build-Agent** is a flexible build Docker image, built on top of [NUKE](https://nuke.build/), designed to standardize and simplify the CI/CD process for various types of applications, especially:

- 🧱 Docker images builds with tagging and registry push
- 📦 Node.js applications (Express, Angular, React, etc.)
- 📁 Artifacts

## ✨ Features

- 🔁 Reusable build logic via NUKE targets
- 📂 Dynamic build and app environment generation from pipeline secrets
- 🪄 Automatic detection of Node.js package managers
- 💬 Discord integration for notifications
- 🚀 GitHub integration for creating releases
- 📜 Orchestration by convention or via `.build.scripts` and `.build.copy`
- 🧪 Dry-run mode for safe testing
- 🟢 Node.js runtime
- 🅰️ Latest Angular and Angular CLI
- 💻 PowerShell for cross-platform scripting
- 🟣 .NET 8 SDK for .NET builds and tools
- 🗃️ Git for source control
- 🔢 GitVersion for semantic versioning in CI/CD
- 📝 **CHANGELOG generation** with Git integration and customizable formatting
- ⚡ **Forge build system** with specialized commands for different project types

## ⚡ Fast Track

Ready to get started? Check out our [Fast Track Guide](fast-track.md) for quick examples, or explore the comprehensive [Build Types Reference](build-types.md) to understand all available build commands and choose the right approach for your project.

For advanced configuration, see [Customization Options](customization.md).

## 🏗️ Architecture & Development

For developers and advanced users, explore our architecture guides:

- **[Multi-Build Architecture](architecture/multi-build-architecture.md)** - Understanding the modular build system
- **[Dependency Injection](architecture/dependency-injection.md)** - Service architecture and testing
- **[Development Guide](architecture/development-guide.md)** - Complete setup and contribution guide
- **[Configuration & Compatibility](architecture/configuration-compatibility.md)** - Environment setup and platform compatibility

<!-- 📦 ⚡ 🛠️ -->