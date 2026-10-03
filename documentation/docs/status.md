---
id: status
title: Status
sidebar_position: 999
---

import Badges from '@site/src/components/Badges';

<Badges />

---

## Understanding Project Status

This page shows live repository badges for the Docker-BuildAgent project. Each badge is rendered from the repository or a workflow status and links to the detail behind it. The badge list is defined in `documentation/src/config/badge-config.ts`; the [README](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/README.md) shows a shorter subset.

Badges that only restate a fixed claim, such as a hard-coded uptime or quality grade, are not shown, because no process in this repository produces those results.

### Badge Categories

#### Build & Release

- **CI and Release**: Status of the `ci.yml` and `release.yml` workflows
- **Coverage**: Link to the coverage report produced by the CI test run

#### Distribution & Deployment

- **Version**: Latest GitHub release
- **Docker**: Link to the container image on GitHub Container Registry
- **Deployments**: GitHub Pages deployments of the documentation site
- **Platform**: Supported hosts (Linux and Windows, macOS best-effort) and the amd64 image architecture, as stated on the [Compatibility and Support](./compatibility.md) page
- **Downloads**: Release download count

#### Documentation

- **Docs**: Link to the documentation site
- **Wiki**: Link to the repository wiki
- **Changelog**: Link to the [changelog](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/documentation/src/pages/CHANGELOG.md)

#### License & Language

- **License**: MIT licence
- **Language**: Top language reported by GitHub

#### Community & Activity

- **Stars, Forks and Contributors**: Repository popularity and contributor count
- **Issues and Pull Requests**: Open issues and a link to pull requests
- **Discussions**: Repository discussions

#### Development Metrics

- **Commits and Last Commit**: Commit activity
- **Code Size and Repo Size**: Repository size statistics
- **Latest Release**: Date of the latest release
- **Lines of Code**: Link to the repository
