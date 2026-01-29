---
id: customization
title: ⚙️ Customization
sidebar_position: 7
---


You can customize the build process by creating a `.build` directory in your project root and adding any of the following files:

| File                | Purpose                                                                                 |
|--------------------------|-----------------------------------------------------------------------------------------|
| [`.app.env.map`](#appenvmap)           | Maps environment variables/constants to your application's `.env` file.                 |
| [`.build.env.map`](#buildenvmap)         | Maps environment variables/constants for the build process (`.build/.build.env`).       |
| [`.build.copy`](#buildcopy)            | Lists files/folders to copy to `ArtifactsDir` after the build completes.                |
| [`.build.scripts`](#buildscripts)         | Lists shell scripts or commands to run as part of the build.                            |
| [`set-environment.ps1`](#environment-variables-helper)    | (Local only) PowerShell script to set environment variables before the build starts.     |

## `.app.env.map`

Defines how environment variables and constants are mapped to your application's `.env` file.

**Example:**

```env
DiscordBotToken=env:DiscordBotToken
DiscordWebhookUrl=env:DiscordWebhookUrl
```

- Use `env:` to pull from the environment.
- Use `const:` to set a constant value.

## `.build.env.map`

Defines environment variables and constants for the build process, generating a `.build/.build.env` file.

**Example:**

```env
RegistryToken=env:GitHubRepositoryToken
NotificationsWebHookUrl=env:BuildNotificationsWebHookUrl
ImageTag=const:build-agent
RegistryUrl=const:ghcr.io/the-running-dev
RegistryUser=const:the-running-dev
CreateGitHubRelease=const:false
```

- Use `env:` to pull from the environment.
- Use `const:` to set a constant value.

## `.build.copy`

Lists files or folders to copy to `ArtifactsDir` after the build completes (one per line).

**Example:**

```env
src/data
.env
package.json
```

## `.build.scripts`

Lists shell scripts or commands to run (one per line).

**Example:**

```env
pwsh script.ps1
npm ci
npm run build:prod
```

## Environment Variables Helper

For local builds, you can create a `set-environment.ps1` script in your project directory. If present, this script is automatically called by `build docker`, `build node`, and `build node-in-docker` before the build starts.

This allows you to set up environment variables or secrets without hardcoding them in your pipeline or Dockerfile.

**Example:**

```pwsh
$env:NODE_ENV = "development"
$env:DISCORD_WEBHOOK_URL = "https://discord.com/api/webhooks/..."
```

> This script runs at the start of the build process, making all variables available to your build and application.

---

**Best Practices:**

- Keep secrets out of source control by using environment variables or secret managers.
- Use `.build.scripts` for repeatable, cross-platform build steps.
- Document your customizations for your team.