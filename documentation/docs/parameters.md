---
id: parameters
title: Parameters
sidebar_position: 3
---

Canonical contract (build command, project configuration): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

Each build type has one table below. A row is one setting, and the columns show every way to set it:

| Column | Meaning |
|---|---|
| Flag | The command-line argument, for example `build docker --image-tag my-app`. A boolean flag takes `true` or `false`. |
| Environment variable | The process environment variable of that name. The same name works as a line in `.build/.build.env.map`, for example `ImageTag=const:my-app`. |
| Config key | The key under `parameters` in the [project configuration file](./project-configuration.md). |
| Default | The value used when nothing sets the setting. |

The flag column shows the kebab-case form, which is also the config key. NUKE ignores case and dashes
when it matches a flag, so `--create-github-release true` and `--create-git-hub-release true` are the same
argument. When one setting is given in several places, the order of precedence is in
[Project Configuration File](./project-configuration.md#precedence).

These tables are generated from the `*Params` classes in `forge/Common/Parameters` and the NUKE `[Parameter]`
fields in `forge/`. Do not edit them by hand. After a parameter changes, run
`pwsh scripts/Update-ParameterDocs.ps1`. The script rewrites the four tables and
`pwsh scripts/Update-ParameterDocs.ps1 -Check` reports whether the page is out of date.

In the tables:

- `none` in a flag or environment variable column means the build type has no such flag. The build does not
  read that setting from an argument or from the environment.
- `none (secret)` in the config key column means the setting is a secret. The project configuration file
  rejects it with `SecretKeyRejected`. Supply it by flag, environment variable or `.build/.build.env.map`.
- `set by the build` means the build computes the value on every run from the repository, the version or
  other settings. The project configuration file accepts the key, but the build overwrites the value, so
  setting it has no effect.
- `empty` is an empty string and `none` in the default column means no default.

## Forge

`build forge` generates the change log. Every other table below starts with the same shared settings as this
one. `--change-log-source` belongs to `build forge` only. The build reads it directly, so it has no config key.

| Flag | Environment variable | Config key | Default |
|---|---|---|---|
| `--notifications` | `Notifications` | `notifications` | `true` |
| `--force-notifications` | `ForceNotifications` | `force-notifications` | `false` |
| `--notifications-web-hook-url` | `NotificationsWebHookUrl` | none (secret) | none |
| `--force-push` | `ForcePush` | `force-push` | `false` |
| `--dry-run` | `DryRun` | `dry-run` | `false` |
| `--verbosity` | `Verbosity` | `verbosity` | `Normal` |
| `--change-log-source` | `ChangeLogSource` | none | none |
| none | none | `config` | set by the build |
| none | none | `root-directory` | set by the build |
| none | none | `repository-url` | set by the build |
| none | none | `version` | set by the build |
| none | none | `change-log-config` | set by the build |

`--notifications` is true unless you pass `--notifications false`. A notification is sent only when a webhook
URL is also set, and only in a CI run (never in a local build or dry run) unless `--force-notifications true`
is given.

### Change log source

`--change-log-source` selects the commits that `GenerateChangeLog` writes:

- Not set, or an empty value: the commits since the last Git tag.
- `all`: the complete commit history.
- Any other value: the commits since that tag, for example `v1.0.0`.

The target writes `CHANGELOG.md` in the project root, puts the new entries before any existing content,
and groups commits by date, latest first, with the date written as `yyyy.MM.dd`. The date format is fixed.

## Docker

| Flag | Environment variable | Config key | Default |
|---|---|---|---|
| `--notifications` | `Notifications` | `notifications` | `true` |
| `--force-notifications` | `ForceNotifications` | `force-notifications` | `false` |
| `--notifications-web-hook-url` | `NotificationsWebHookUrl` | none (secret) | none |
| `--force-push` | `ForcePush` | `force-push` | `false` |
| `--dry-run` | `DryRun` | `dry-run` | `false` |
| `--verbosity` | `Verbosity` | `verbosity` | `Normal` |
| `--templates-dir` | `TemplatesDir` | `templates-dir` | `/nuke/templates` |
| `--docker-file` | `DockerFile` | `docker-file` | `Dockerfile` |
| `--image-tag` | `ImageTag` | `image-tag` | `container-app` |
| `--registry-url` | `RegistryUrl` | `registry-url` | empty |
| `--registry-user` | `RegistryUser` | none (secret) | empty |
| `--registry-token` | `RegistryToken` | none (secret) | empty |
| `--create-git-hub-release` | `CreateGitHubRelease` | `create-git-hub-release` | `false` |
| `--pre-release` | `PreRelease` | `pre-release` | `false` |
| none | none | `config` | set by the build |
| none | none | `root-directory` | set by the build |
| none | none | `repository-url` | set by the build |
| none | none | `version` | set by the build |
| none | none | `change-log-config` | set by the build |
| none | none | `tags` | set by the build |
| none | none | `release-tag` | set by the build |

`build docker` has no `--tags` flag. The image tags come from `--image-tag`, `--registry-url` and the
version on every run. With `--create-github-release true` the image gets both a `latest` tag and a version
tag. Without it, only `latest`. The release tag is always `v` followed by the version.

## Node

| Flag | Environment variable | Config key | Default |
|---|---|---|---|
| `--notifications` | `Notifications` | `notifications` | `true` |
| `--force-notifications` | `ForceNotifications` | `force-notifications` | `false` |
| `--notifications-web-hook-url` | `NotificationsWebHookUrl` | none (secret) | none |
| `--force-push` | `ForcePush` | `force-push` | `false` |
| `--dry-run` | `DryRun` | `dry-run` | `false` |
| `--verbosity` | `Verbosity` | `verbosity` | `Normal` |
| `--artifacts-dir` | `ArtifactsDir` | `artifacts-dir` | `artifacts` |
| none | none | `config` | set by the build |
| none | none | `root-directory` | set by the build |
| none | none | `repository-url` | set by the build |
| none | none | `version` | set by the build |
| none | none | `change-log-config` | set by the build |

`build node` declares the Docker flags too (`--templates-dir`, `--registry-url`, `--registry-user`,
`--registry-token`, `--image-tag`, `--docker-file`), but it never reads them and they are not in the table.

## Node in Docker

| Flag | Environment variable | Config key | Default |
|---|---|---|---|
| `--notifications` | `Notifications` | `notifications` | `true` |
| `--force-notifications` | `ForceNotifications` | `force-notifications` | `false` |
| `--notifications-web-hook-url` | `NotificationsWebHookUrl` | none (secret) | none |
| `--force-push` | `ForcePush` | `force-push` | `false` |
| `--dry-run` | `DryRun` | `dry-run` | `false` |
| `--verbosity` | `Verbosity` | `verbosity` | `Normal` |
| `--templates-dir` | `TemplatesDir` | `templates-dir` | `/nuke/templates` |
| `--docker-file` | `DockerFile` | `docker-file` | `Dockerfile` |
| `--image-tag` | `ImageTag` | `image-tag` | `container-app` |
| `--registry-url` | `RegistryUrl` | `registry-url` | empty |
| `--registry-user` | `RegistryUser` | none (secret) | empty |
| `--registry-token` | `RegistryToken` | none (secret) | empty |
| `--create-git-hub-release` | `CreateGitHubRelease` | `create-git-hub-release` | `false` |
| none | none | `pre-release` | `false` |
| `--artifacts-dir` | `ArtifactsDir` | `artifacts-dir` | `artifacts` |
| none | none | `config` | set by the build |
| none | none | `root-directory` | set by the build |
| none | none | `repository-url` | set by the build |
| none | none | `version` | set by the build |
| none | none | `change-log-config` | set by the build |
| none | none | `tags` | set by the build |
| none | none | `release-tag` | set by the build |

`build node-in-docker` has no `--pre-release` flag. The `pre-release` key is accepted in the project
configuration file, but nothing in this build type reads it, so its GitHub release is never marked as a
pre-release. The image always gets both a `latest` tag and a version tag.

## Example

Tell the build to create a GitHub release (`false` by default). Use a flag:

```pwsh
& docker run `
     -e DOCKER_HOST=tcp://host.docker.internal:2375 `
     -v ./:/workspace `
     -it ghcr.io/the-running-dev/build-agent:latest `
     build docker --create-github-release true
```

Or put it in `.build/.build.env.map`:

```env
CreateGitHubRelease=const:true
```

Or set it in `buildagent.yml`:

```yaml
schemaVersion: 1
buildType: docker
parameters:
  create-git-hub-release: true
```
