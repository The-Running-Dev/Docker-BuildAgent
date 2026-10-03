---
id: troubleshooting
title: Troubleshooting and FAQ
sidebar_position: 13
---

Canonical contract (build command): [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)

This page covers common problems with `build` and the answers to frequent questions. The first thing to check is
the exit status. [Exit Codes](./exit-codes.md) says what each status means.

## Troubleshooting

- **The build ends with status 2 before any step runs:**
  - The project configuration file is invalid. Read the `Configuration error:` lines. Each names the file, the key
    and the rule. See [Project Configuration File](./project-configuration.md#invalid-configuration).
- **The build ends with status 255:**
  - A target failed. Find the target marked as failed in the summary at the end of the output and read the error above it.
  - `Build Env Incomplete` means a variable in `.build/.build.env.map` has no value. Set it, or remove the line.
- **The image is built but not pushed:**
  - `PushToRegistry` runs only in CI. It is skipped in a local build and in a dry run. Pass `--force-push true`
    to push anyway.
- **`RegistryToken is Not Set`:**
  - `PushToRegistry` needs a token. Give it as `--registry-token`, as the variable `RegistryToken` or as a line in
    `.build/.build.env.map`. The project configuration file does not accept it.
- **No GitHub release is created:**
  - `PublishToGitHub` needs all of these: `--create-github-release true`, a CI run or `--force-push true`, no
    dry run, a repository with an HTTPS remote URL, and a non-empty `RegistryToken`. If one is missing the target
    is skipped without an error.
- **Docker login or push fails:**
  - Check that `RegistryToken` is valid and has the right permissions, for GitHub Container Registry
    `write:packages`.
  - Pass secrets from your CI system's secret store.
- **The Docker daemon is unreachable:**
  - Mount the socket with `-v /var/run/docker.sock:/var/run/docker.sock`, or set `DOCKER_HOST` to a reachable daemon.
- **`No Dockerfile Template exists for application type`:**
  - The project has no Dockerfile at the configured path, and no `Dockerfile.<type>` exists for the detected type.
    The image ships templates for `angular` and `node` only. See [Docker Templates](./docker-templates.md).
- **An unknown build type is rejected:**
  - The first argument of `build` must be `docker`, `node`, `node-in-docker`, `node-template` or `forge`.
- **GitVersion or the Nuke tool is not found:**
  - The image installs both as .NET global tools in `/root/.dotnet/tools` and adds that directory to `PATH`. Make
    sure the container runs as root and that your command does not replace `PATH`.
- **.NET build issues outside the container:**
  - Building the Forge solution from source needs the .NET 8 SDK or later. Inside the image the .NET 8, 9 and 10 SDKs are installed.
- **Changelog generation issues:**
  - The default source is the commits since the last tag, so the change log is empty when there are none. Check
    `git tag -l` and use `--change-log-source all` for the complete history.
  - Check that the project directory is writable, because the target writes `CHANGELOG.md` there.
- **Date formatting problems:**
  - The change log writes dates as `yyyy.MM.dd`. The format cannot be changed.
  - Check that the commit dates in the Git history can be parsed.

## FAQ

- **Q: I get a permission denied error on `build.sh` in my clone of this repository.**
  - A: Run `chmod +x build.sh` on Linux or macOS. On Windows, check the PowerShell execution policy for `build.ps1`.
    Both scripts build this repository from source. Inside the image you run `build <type>` instead.
- **Q: How do I pass secrets to a build?**
  - A: As flags, as environment variables or in `.build/.build.env.map`. The project configuration file rejects
    secrets. Never write them in scripts or Dockerfiles.
- **Q: How do I run a build for a specific project type?**
  - A: Give the type as the first argument: `build docker`, `build node`, `build node-in-docker`,
    `build node-template` or `build forge`. See [Build Types](./build-types.md).
- **Q: How do I run only part of a build?**
  - A: Pass `--target` with a target name, for example `build docker --target BuildDockerImage`. That target and
    the targets before it run. The targets of each type are in [Targets](./targets.md).
- **Q: What does `build docker` run, in what order?**
  - A: `BuildDockerImage`, `PushToRegistry`, `PublishToGitHub` and `Build`. The push and the release are skipped
    outside CI unless `--force-push true` is given.
- **Q: Why is my change log empty or not generating correctly?**
  - A: There are no commits since the last tag. Use `--change-log-source all` to generate the complete history, or
    check the last tag with `git tag -l`.
- **Q: Can I customize the change log date format?**
  - A: No. The format is `yyyy.MM.dd`.

For more help, see the project README or open an issue on GitHub.
