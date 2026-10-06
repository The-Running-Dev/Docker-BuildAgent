# Docker-BuildAgent PowerShell Module Requirements

Canonical for: PowerShell module.

## Purpose

Define a reusable requirements specification for the Docker-BuildAgent PowerShell module so it can be maintained, re-implemented, or ported without relying on scattered documentation.

## Scope

- In scope: module interface, configuration model, argument translation, validation, execution behavior, parameter metadata generation, and security/logging constraints.
- Out of scope: NUKE build internals, Dockerfile template logic, and non-module scripts except where consumed by the module.

## Public API Requirements

### R-API-001: Exported members

- The module must export only:
  - Function: Set-BuildAgentConfig
  - Function: Invoke-Build
  - Variable: BuildAgentConfig
- No aliases or cmdlets are exported by the module manifest.

### R-API-002: Module compatibility

- PowerShell minimum version must be 5.1.
- Module manifest must keep metadata versioned (module version, GUID, description, author).

## Configuration Requirements

### R-CONFIG-001: Stateful config object

- The module must maintain a script-scoped configuration object that persists for the session.
- Required fields:
  - DockerImage
  - DockerHost
  - WorkspacePath
  - ArtifactsDir
  - Environment
  - Parameters (hashtable)

### R-CONFIG-002: Default values

- Defaults must include:
  - DockerImage = ghcr.io/the-running-dev/build-agent:<module version>, the module's own `ModuleVersion` from the manifest (for example `2.0.0`), never `latest`
  - DockerHost = tcp://host.docker.internal:2375
  - WorkspacePath = module path
  - ArtifactsDir = artifacts
  - Environment = development
  - Parameters = empty hashtable
- The default image reference is overridable with Set-BuildAgentConfig -DockerImage, and an override must be used verbatim.

### R-CONFIG-003: Set-BuildAgentConfig validation

- Set-BuildAgentConfig must validate:
  - DockerImage is required
  - DockerHost is required and must support these patterns:
    - tcp://host:port
    - unix:///path
    - npipe:////./pipe/name
  - WorkspacePath is required and must exist as a directory; otherwise it must fail with WorkspaceInvalid (exit status 3)
  - A relative WorkspacePath must be resolved against the current directory and stored absolute, so the mount never reaches docker as a bare name it would read as a named volume
  - Environment must be one of development or production
- AdditionalParameters must be optional and default to empty hashtable.

### R-CONFIG-004: Config update behavior

- Set-BuildAgentConfig must replace existing values in the script-scoped config object.
- It must emit a success message after applying configuration.

## Build Invocation Requirements

### R-INVOKE-001: Supported build types

- Invoke-Build must accept only:
  - docker
  - node
  - node-in-docker
  - node-template
  - forge

### R-INVOKE-002: Argument merge order

- Invoke-Build must merge arguments with this precedence:
  - Base: config.Parameters
  - Override: Invoke-Build -args values
- Caller-provided args always win.

### R-INVOKE-003: Parameter name conversion

- Hashtable keys must be transformed from camelCase/PascalCase to kebab-case CLI flags.
  - Example: imageTag -> --image-tag
- Scalar values produce one flag/value pair.
- Enumerable non-string values must produce repeated flag/value pairs.
  - Example: tags=[a,b] -> --tags a --tags b
- Null values must be skipped.

### R-INVOKE-004: Container command contract

- Invoke-Build must execute docker run with:
  - --rm
  - mounted workspace to /workspace
  - working directory /workspace
  - DOCKER_HOST environment variable
  - configured image
  - command prefix `build <type>`
  - converted user arguments

### R-INVOKE-005: Exit behavior

- On non-zero docker exit code, Invoke-Build must throw a terminating error, with FullyQualifiedErrorId `BuildFailed`, carrying the container's exit status unchanged in `Exception.Data['ExitCode']`. It must not map one status onto another (I28).
- On success, no exception is thrown.

### R-INVOKE-006: Launcher errors

- Every launcher error is a terminating error whose FullyQualifiedErrorId is its code and whose `Exception.Data['ExitCode']` is its process exit status:
  - `WorkspaceInvalid`, exit 3: the workspace path is absent or not a directory. Checked by Set-BuildAgentConfig and again by Invoke-Build before docker is called.
  - `DockerUnavailable`, exit 5: the Docker daemon cannot be reached.
  - `ImageUnavailable`, exit 5: the configured image is not present locally and cannot be pulled. Invoke-Build must not fall back to another version or to `latest` (I27) and must not run the build.
  - `BuildFailed`: see R-INVOKE-005.

## Validation Requirements

### R-VALIDATE-001: Optional argument validation

- Invoke-Build must support optional -validateArgs.
- When enabled, argument keys must be validated against allowed parameter names for the selected build type.

### R-VALIDATE-002: parameters.json source

- Validation metadata must be read from parameters.json in the module directory.
- JSON loading must use raw file read to support pretty-printed multi-line JSON.

### R-VALIDATE-003: Missing metadata behavior

- If parameters.json is missing, validation must not fail invocation by default and should act as no allow-list.
- Today parameters.json is generated by Update-ModuleParameters.ps1 but is not committed, is not part of the module and is not produced by any build or release step. A module installed from this repository therefore has no parameters.json, and -validateArgs is a silent no-op: it reports nothing and accepts every key, including unknown ones. R-VALIDATE-004 holds only when a parameters.json has been generated into the module directory by hand.

### R-VALIDATE-004: Unknown argument behavior

- When validation is active and unknown keys are found, Invoke-Build must throw and list unknown parameter names.

## Parameter Extraction Script Requirements

### R-EXTRACT-001: Discovery

- Update-ModuleParameters.ps1 must scan forge/Common/Parameters for C# files.

### R-EXTRACT-002: Metadata parsing

- The extractor must parse:
  - Class name ending in Params
  - Property Name, Type, Description from XML summary comments
- Supported property type patterns must include:
  - Namespaced types
  - Nullable types
  - Arrays
  - Simple generic forms

### R-EXTRACT-003: Inheritance merge

- If a parameter class inherits from another Params class, base parameters must be prepended/merged into child parameters.

### R-EXTRACT-004: Output format

- Output must be JSON with sufficient depth to preserve nested objects and arrays.
- Output file default is parameters.json in module directory.

### R-EXTRACT-005: Failure behavior

- If parameter directory does not exist, script must fail fast with explicit error.

## Security and Compliance Requirements

### R-SEC-001: Safe logging

- Build invocation must not log full docker command line with argument values.
- Logs must avoid accidental secret disclosure from forwarded args.

### R-SEC-002: Token handling

- Tokens passed through args or config must only be forwarded to docker command execution, not echoed in clear text.

## Documentation-Behavior Alignment Requirements

### R-DOC-001: Canonical workflow

- Module behavior must remain aligned with unified build command semantics: `build <type> [args]`.

### R-DOC-002: Reusability guidance

- Documentation/spec should preserve migration guidance from direct docker run usage to module-driven invocation.

## Non-Functional Requirements

### R-NFR-001: Cross-platform host support

- DockerHost validation must support Windows and Linux daemon endpoint formats.

### R-NFR-002: Deterministic invocation

- Given same config and args, generated docker argument list must be deterministic for scalar arguments.

### R-NFR-003: Backward compatibility

- Public function names and accepted build types must remain stable across minor versions unless a documented breaking change is introduced.

## Acceptance Criteria Checklist

- Set-BuildAgentConfig rejects invalid DockerHost values and accepts tcp, unix, npipe formats.
- Invoke-Build runs docker with `build <type>` and mapped args.
- Invoke-Build throws on non-zero docker exit code.
- validateArgs is a silent no-op when parameters.json is absent from the module directory, which is the case for the module as shipped (R-VALIDATE-003).
- When a generated parameters.json is present, validateArgs passes when keys are known and fails when unknown, and a pretty-printed parameters.json parses successfully. Nothing in the repository's tests exercises this path yet.
- Update-ModuleParameters.ps1 captures generic and nullable property types.
- Logs do not expose full raw argument string.
- Only Set-BuildAgentConfig, Invoke-Build, and BuildAgentConfig are exported.

## Suggested Future Enhancements

- Add formal JSON schema for parameters.json.
- Extend the existing Pester suite (Docker-BuildAgent.Tests.ps1, run in CI on Windows PowerShell 5.1 and PowerShell 7) to cover argument conversion and validation behavior.
- Ship or generate parameters.json with the module so that -validateArgs does something, or remove the switch.
- Add deterministic ordering tests for enumerable argument expansion.
- Add stricter repository/document synchronization checks for module docs.
