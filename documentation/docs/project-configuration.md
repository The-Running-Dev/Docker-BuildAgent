---
id: project-configuration
title: "Project Configuration File"
sidebar_position: 4
---

# Project Configuration File

A project can keep its build settings in a file at its root instead of repeating them as
arguments on every build. The build reads the file before it runs, checks it, and uses its values
for every parameter you did not set some other way.

:::note Not yet released
The project configuration file is part of 2.0.0, which has not been released. See the
[2.0.0 release notes](./release-notes/2.0.0.md).
:::

## File name and location

Put one of these in the project root (the directory mounted at `/workspace`):

- `buildagent.yml`
- `buildagent.yaml`
- `buildagent.json`

Only one may exist. If two or more are present, the build stops with `MultipleConfigurationFiles`
rather than choosing between them. If none exists, nothing changes: the build runs as it always has.

The JSON form is the same document as the YAML form. The build only ever reads the file. It never
creates or changes it.

## Shape

```yaml
schemaVersion: 1
buildType: docker
parameters:
  repository-url: https://github.com/example/my-project.git
  docker-file: Dockerfile
  image-tag: my-app
  registry-url: ghcr.io/example
  create-git-hub-release: false
  notifications: false
```

| Key | Required | Meaning |
|---|---|---|
| `schemaVersion` | yes | Must be `1`. A missing or different value is rejected. |
| `buildType` | yes | One of `docker`, `node`, `node-in-docker`, `node-template`, `forge`. |
| `parameters` | no | The build's parameters, as a map. |

Rules for the document:

- Keys under `parameters` are the **kebab-case** form of the build type's parameter names:
  `DockerFile` is `docker-file`, `CreateGitHubRelease` is `create-git-hub-release`. The names are
  the same ones the command line takes. See [Parameters](./parameters.md) for each build type's
  list and defaults.
- `node-template` has no parameters of its own, so any key under `parameters` for that type is
  rejected. The `node-template` build runs through its own PowerShell flow and does not read or
  validate the file today, so a file for it has no effect on that build.
- YAML anchors, aliases, several documents in one file, and keys that are not strings are
  rejected.
- A value must fit its parameter: a boolean parameter takes `true` or `false`, a list parameter
  such as `tags` takes a list.
- `repository-url` may be left out. When nothing sets it, the build derives it from the project's
  git remote.

Working samples for each build type are in
[`samples/`](https://github.com/The-Running-Dev/Docker-BuildAgent/tree/main/samples), in both YAML
and JSON.

## Secrets are never read from the file

These parameters carry credentials, and the file may not set them:

- `registry-user`
- `registry-token`
- `notifications-web-hook-url`

A file that names one is rejected with `SecretKeyRejected`. Supply secrets by argument, by
environment variable (the variable name is the parameter's PascalCase name, for example
`RegistryToken`), or through the `.build/.build.env.map` mapping file.

## Precedence

When the same parameter is set in more than one place, the highest of these wins:

1. An argument on the command line
2. PowerShell module configuration (`Set-BuildAgentConfig`)
3. The process environment
4. The environment generated from `.build/.build.env.map`
5. The project configuration file
6. The parameter's declared default

The file therefore fills in what nothing stronger sets, and any argument or environment variable
still overrides it for a single run.

## Invalid configuration

Every problem found in the file is collected and reported together, then the build exits with
status **2** and runs no step. Each line has this form:

```text
Configuration error: file=/workspace/buildagent.yml key=image-tag rule=ValueTypeMismatch: ...
```

| Rule | Cause |
|---|---|
| `MultipleConfigurationFiles` | More than one of the three file names exists. |
| `FileUnreadable` | The file exists but could not be read. This is the only rule worth retrying. |
| `MalformedDocument` | The file is not valid, or uses anchors, aliases, several documents or non-string keys. |
| `SchemaVersionMissing` / `SchemaVersionUnsupported` | `schemaVersion` is absent, or is not `1`. |
| `UnknownBuildType` | `buildType` is absent or not one of the five types. |
| `UnknownKey` | A key under `parameters` is not a parameter of the build type. The message names the nearest known key. |
| `SecretKeyRejected` | The file sets one of the secret parameters above. |
| `ValueTypeMismatch` | A value does not fit the parameter's type. |
| `RequiredValueMissing` | A required parameter has no value from any tier. |

Exit status 2 passes through the PowerShell module unchanged. The full table of exit statuses is
in the contract.

Canonical contract: [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
