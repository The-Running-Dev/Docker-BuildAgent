# Samples

Sample [project configuration files](https://build-agent.subzerodev.com/docs/project-configuration), one
folder per build type: `docker`, `forge`, `node`, `node-in-docker` and `node-template`. Each folder has the
same document in YAML (`buildagent.yml`) and in JSON (`buildagent.json`).

To use one, copy a single file to the root of your project, the directory you mount at `/workspace`, and
edit the values. Keep only one of `buildagent.yml`, `buildagent.yaml` and `buildagent.json` there: with
more than one the build stops with `MultipleConfigurationFiles`.

Keys under `parameters` are the kebab-case names of the build type's parameters, listed in
[Parameters](https://build-agent.subzerodev.com/docs/parameters). The file never carries secrets
(`registry-user`, `registry-token`, `notifications-web-hook-url`); it is rejected if it names one.

The sample values (`my-app`, `ghcr.io/example`, the example repository URL) are placeholders. The
`node-template` sample has no parameters because that build type has none, and that build does not read
the file today.
