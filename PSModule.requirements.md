# Docker-BuildAgent PowerShell Module Requirements

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
	- DockerImage = ghcr.io/the-running-dev/build-agent:latest
	- DockerHost = tcp://host.docker.internal:2375
	- WorkspacePath = module path
	- ArtifactsDir = artifacts
	- Environment = development
	- Parameters = empty hashtable

### R-CONFIG-003: Set-BuildAgentConfig validation
- Set-BuildAgentConfig must validate:
	- DockerImage is required
	- DockerHost is required and must support these patterns:
		- tcp://host:port
		- unix:///path
		- npipe:////./pipe/name
	- WorkspacePath is required and must exist as a directory
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
	- command prefix build <type>
	- converted user arguments

### R-INVOKE-005: Exit behavior
- On non-zero docker exit code, Invoke-Build must throw with exit code details.
- On success, no exception is thrown.

## Validation Requirements

### R-VALIDATE-001: Optional argument validation
- Invoke-Build must support optional -validateArgs.
- When enabled, argument keys must be validated against allowed parameter names for the selected build type.

### R-VALIDATE-002: parameters.json source
- Validation metadata must be read from parameters.json in the module directory.
- JSON loading must use raw file read to support pretty-printed multi-line JSON.

### R-VALIDATE-003: Missing metadata behavior
- If parameters.json is missing, validation must not fail invocation by default and should act as no allow-list.

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
- Module behavior must remain aligned with unified build command semantics: build <type> [args].

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
- Invoke-Build runs docker with build <type> and mapped args.
- Invoke-Build throws on non-zero docker exit code.
- validateArgs passes when keys are known and fails when unknown.
- parameters.json pretty-printed file parses successfully during validation.
- Update-ModuleParameters.ps1 captures generic and nullable property types.
- Logs do not expose full raw argument string.
- Only Set-BuildAgentConfig, Invoke-Build, and BuildAgentConfig are exported.

## Suggested Future Enhancements
- Add formal JSON schema for parameters.json.
- Add Pester tests for argument conversion and validation behavior.
- Add deterministic ordering tests for enumerable argument expansion.
- Add stricter repository/document synchronization checks for module docs.

