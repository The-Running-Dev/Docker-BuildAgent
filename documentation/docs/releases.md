---
id: releases
title: "🚀 Release Management"
sidebar_position: 9
---

# Release Management

This guide explains how the Docker Build Agent project handles releases and versioning to maintain clean release history and distinguish between development builds and official releases.

## Release Strategy Overview

The project uses a **controlled release strategy** with three main workflows:

### Development Workflow

- **Purpose**: Continuous integration and deployment for development
- **Trigger**: Every push to `main` branch
- **Workflow**: `.github/workflows/release.yml` (Deploy)
- **Output**: Docker images published to registry (no GitHub releases)

### Release Workflow

- **Purpose**: Create official releases for users
- **Trigger**: Manual dispatch or version tags
- **Workflows**: 
  - `.github/workflows/create-release.yml` (Manual)
  - `.github/workflows/tag-release.yml` (Tag-based)
- **Output**: GitHub releases with changelog, Docker images, and Git tags

### Validation Workflow

- **Purpose**: Validate pull requests and feature branches
- **Trigger**: Pull requests, feature branch pushes
- **Workflow**: `.github/workflows/ci.yml` (CI)
- **Output**: Test results and validation (no deployments)

## Creating Releases

### Method 1: Manual Release (Recommended)

This is the **preferred method** for creating releases as it gives you full control:

1. **Navigate to Actions**
   - Go to your repository's **Actions** tab
   - Select **"Create Release"** workflow

2. **Configure Release**
   - Click **"Run workflow"**
   - Optionally specify:
     - **Version**: Custom version (e.g., `v1.2.3`) or leave empty for auto-version
     - **Pre-release**: Check if this is a beta/RC release
     - **Release notes**: Custom release notes

3. **Execute**
   - Click **"Run workflow"** button
   - The workflow will run tests, build Docker images, and create the GitHub release

### Method 2: Tag-Based Release

For automated releases based on Git tags:

```bash
# Create a normal release
git tag v1.2.3
git push origin v1.2.3

# Create a pre-release
git tag v1.0.0-beta.1
git push origin v1.0.0-beta.1

# Create a release candidate
git tag v2.0.0-rc.1
git push origin v2.0.0-rc.1
```

**Pre-release Detection**: Tags containing `-alpha`, `-beta`, `-rc`, or other suffixes are automatically marked as pre-releases.

## Versioning Strategy

### GitVersion Configuration

The project uses **GitVersion** in `ContinuousDelivery` mode with the following configuration:

```yaml
mode: ContinuousDelivery
branches:
  main:
    increment: Minor
    is-release-branch: false
```

### Version Increment Rules

- **Main branch commits**: Minor version increment (e.g., 1.0.0 → 1.1.0)
- **Feature branches**: Inherit increment from source branch
- **Release branches**: Patch increment (e.g., 1.1.0 → 1.1.1)  
- **Hotfix branches**: Patch increment

### Pre-release Versions

Pre-release versions are automatically generated for:

- Feature branches: `1.1.0-feature-branch.1`
- Pull requests: `1.1.0-pr-123.1`
- Release candidates: `1.1.0-rc.1`

## Release Content

### What Gets Included

Each official release includes:

1. **GitHub Release**
   - Semantic version tag (e.g., `v1.2.3`)
   - Auto-generated changelog from commit history
   - Docker image information
   - Release artifacts

2. **Docker Images**
   - Published to GitHub Container Registry (GHCR)
   - Tagged with version number and `latest`
   - Multi-architecture support (if configured)

3. **Git Tag**
   - Created automatically after successful release
   - Follows semantic versioning format

### Changelog Generation

Changelogs are automatically generated from Git commit history using:

- **Commit messages** since the last release
- **Pull request titles** and descriptions
- **Custom formatting** with date stamps
- **Docker image references** for each release

## Build Parameters

### Release-Specific Parameters

When creating releases, you can use these parameters:

```bash
# Create a release
nuke --type docker --create-github-release true

# Create a pre-release
nuke --type docker --create-github-release true --pre-release true

# Custom version override
nuke --type docker --create-github-release true --version v1.2.3

# Dry run mode (testing)
nuke --type docker --create-github-release true --dry-run true
```

### Environment Variables

Required for release creation:

- `GITHUB_TOKEN`: GitHub authentication token
- `RegistryToken`: Container registry authentication
- `NotificationsWebHookUrl`: Discord/Slack notifications (optional)

## Best Practices

### When to Create Releases

✅ **Do create releases for:**

- New features ready for users
- Bug fixes that affect users
- Breaking changes
- Security updates
- Documentation updates that affect usage

❌ **Don't create releases for:**

- Internal refactoring
- CI/CD changes
- Development dependencies updates
- Code formatting changes

### Release Naming

- **Major**: Breaking changes (v1.0.0 → v2.0.0)
- **Minor**: New features, backward compatible (v1.0.0 → v1.1.0)  
- **Patch**: Bug fixes, backward compatible (v1.0.0 → v1.0.1)
- **Pre-release**: Beta/RC versions (v1.0.0-beta.1)

### Testing Before Release

The release workflows automatically:

1. Run comprehensive test suites
2. Generate code coverage reports
3. Validate Docker image builds
4. Check for security vulnerabilities
5. Verify documentation builds

## Troubleshooting

### Common Issues

**Release creation fails**

- Check that `GITHUB_TOKEN` has correct permissions
- Verify `RegistryToken` is valid for container registry
- Ensure all tests pass before release

**Version conflicts**

- Avoid creating multiple releases with same version
- Use pre-release versions for testing
- Check GitVersion configuration for correct incrementing

**Missing changelog**

- Ensure commits follow conventional commit format
- Check that there are commits since last release
- Verify Git history is available (fetch-depth: 0)

### Manual Recovery

If a release fails partially:

```bash
# Clean up failed release
gh release delete v1.2.3 --yes

# Recreate release manually
git tag -d v1.2.3
git push origin :v1.2.3
git tag v1.2.3
git push origin v1.2.3
```

## Migration from Previous Strategy

If you previously created releases on every commit, here's how to clean up:

1. **Review existing releases** - Delete development/test releases
2. **Update workflows** - Use the new release strategy workflows  
3. **Update documentation** - Inform users about the new release cadence
4. **Clean tags** - Remove unnecessary version tags if needed

The new strategy provides:

- ✅ Cleaner release history
- ✅ Meaningful release notes  
- ✅ Better user experience
- ✅ Controlled deployment process
- ✅ Separation of development and production builds
