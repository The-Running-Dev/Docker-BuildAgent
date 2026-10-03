# Security Policy

## Supported versions

Fixes ship only in the current major version, and on `main`. Earlier versioned images stay pullable,
because their tags are immutable, but they are not patched. See
[Compatibility and Support](https://build-agent.subzerodev.com/docs/compatibility).

## Reporting a vulnerability

Do not open a public issue for a security problem.

Email **ben@subzerodev.com** with:

- what the problem is and which component it affects (the image, the `build` command, the PowerShell
  module, a workflow);
- the version or commit, and the steps to reproduce it;
- the impact you expect.

GitHub private vulnerability reporting is not enabled for this repository, so email is the only private
channel. Please allow a reasonable time for a reply; this is a single-maintainer project and there is no
response-time commitment.

## Scope

In scope: the container image, the Forge build system, the `build` command, the PowerShell module, and
the workflows in this repository.

Out of scope: the documentation site template (report it to
[Docusaurus-Template](https://github.com/The-Running-Dev/Docusaurus-Template)), and vulnerabilities in
third-party tools shipped in the image (report those upstream; tell us if a fixed version should be
picked up).

## Secrets

Build secrets such as `RegistryToken` and the notifications webhook URL are supplied by argument,
environment variable or the `.build/.build.env.map` file. The project configuration file rejects them.
If you find a path where a secret is logged or written to disk, report it as a vulnerability.
