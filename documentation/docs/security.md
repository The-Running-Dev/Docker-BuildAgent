---
id: security
title: "Security Model"
sidebar_position: 16
---

# Security Model

Docker-BuildAgent is a build environment. It runs the code you give it, with the access you give
it. This page states what the image trusts, what it protects, and where its protection ends. To
report a vulnerability, follow the
[security policy](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/SECURITY.md).

## What the image trusts

- **The code it builds.** A build runs the project's own scripts, package installs and Dockerfiles.
  Run the image only on code you trust.
- **Its user is root.** The image sets no `USER`, so builds run as root inside the container. Do
  not rely on the container as a boundary between the build and anything you mount into it.
- **The Docker socket, when you mount it.** The `docker` and `node-in-docker` builds need the host
  Docker daemon, through the mounted socket or `DOCKER_HOST`. Mounting the socket gives the
  build control of the host's Docker daemon, which is control of the host. Mount the socket only
  on a host you trust, for code you trust. A host compromised through a mounted socket is not a
  defect in the product.
- **The workspace.** Mount only the project directory as `/workspace`. Anything else you mount is
  readable and writable by the build.

## Secrets

- Supply secrets such as `RegistryToken`, `GITHUB_TOKEN` and the notifications webhook URL as
  arguments, environment variables or entries in `.build/.build.env.map`.
- The project configuration file rejects secrets: a token in `buildagent.yml` fails validation.
- Build output strips GitHub personal access tokens and URL hosts from the values it displays,
  and the parameter display leaves out `RegistryToken`. The PowerShell module prints "arguments
  hidden for security" instead of the argument list.
- A path that logs a secret or writes one to disk is a vulnerability. Report it.

## Image vulnerabilities

Every pull request builds the image and scans it with Grype. The scan lists the high and critical
vulnerabilities that have a fix available; see [CI/CD](./ci-cd.md). It reports and does not fail
the pull request, because the image currently carries fixable critical findings, from the base
image's Perl packages and from globally installed Node.js packages.

Remediation is by rebuilding, not by patching inside the image:

- Findings in the base image are fixed by moving `BASE_IMAGE` to a newer
  `javascript-node:22-bookworm` digest.
- Findings in Node.js tools are fixed by raising their version arguments in the `Dockerfile`.
- Once a pull request's scan reports no fixable high or critical findings, the scan becomes a gate.

Fixes ship in the current major version only; see [Compatibility and Support](./compatibility.md).
Vulnerabilities in third-party tools shipped in the image belong upstream; tell us when a fixed
version should be picked up.

Canonical contract: [design/20-contract.md](https://github.com/The-Running-Dev/Docker-BuildAgent/blob/main/design/20-contract.md)
