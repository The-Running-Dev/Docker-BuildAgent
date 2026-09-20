# Contract — Docker-BuildAgent v2.0.0

Derived from `design/10-design.md`. This document constrains every implementing
session; downstream work is checked against it.

**How to read an entry.** Where the declaration exists in the tree, the entry points
at it and states only what the declaration cannot carry. Where it does not exist, the
entry carries a scaffold — declarations in the project's language, types and
signatures only, no bodies. A slice that materialises a scaffold replaces it with a
pointer in the same commit.

**Language and nullability.** C# declarations are `#nullable enable`: a reference type
without `?` is non-null and construction rejects null. No `object`, no `dynamic`, no
untyped dictionary crosses a module boundary. PowerShell declarations carry explicit
parameter types and `[ValidateSet]` where the accepted set is closed.

---

## Invariants

Each invariant names the module responsible for maintaining it and its enforcement
today. **`[code]`** means a reader may trust it without checking, and cites the
enforcing declaration. **`[instruction]`** means nothing enforces it yet; a reader
must check, and the slice that lands the owning module makes it `[code]`.

The `Id` column is also the invariant's design-state unit id, and the rows below are
the invariant unit set per § *Artifacts of a unit kind*. Ids are permanent and
sequential across every subsection; the subsection heading carries the domain, which
is why no id does. Ids are never reused, so the numbering may carry gaps.

### Release and versioning

| Id | Invariant | Owner | Enforcement |
|---|---|---|---|
| **I1** | **One version, three sinks.** A release stamps exactly one version value into the versioned image tag, the global tool package and the PowerShell module manifest. No sink derives, defaults or increments its own version. | Release pipeline | `[instruction]` |
| **I2** | **A published versioned image tag is immutable.** From v2.0.0 onward, a versioned tag's content never changes and the tag is never deleted or expired. | Release pipeline | `[instruction]` — the registry does not enforce it, and the design does not rely on the registry to |
| **I3** | **No sink is written before the claim exists.** Version selection, existence checking, the surface gate and notes validation write nothing. The first write of a release is the claim. | Release pipeline | `[instruction]` |
| **I4** | **A partial release never moves `latest`.** `latest` moves only after every versioned sink holds the version. **In tension with the step order in `10-design.md` § Control flow 2; see § Unresolved U-9.** | Release pipeline | `[instruction]` |
| **I5** | **A version that exists is never republished.** Existence is the disjunction of: a claim for that version, a versioned image tag for that version, or a git tag for that version. Any one of the three makes the version taken, including when the operator supplies it manually. | Release pipeline | `[instruction]` |
| **I6** | **Major never decreases.** A candidate whose major is below the highest published major fails before any write. | Release pipeline | `[instruction]` |
| **I7** | **Notes carry both required sections.** Every published release's notes contain a breaking-changes section and a deprecations section, present when empty. A release cannot publish without them. | Release pipeline | `[instruction]` |
| **I8** | **The baseline is what shipped.** The surface gate's baseline is the manifest asset of the highest published release below the candidate. It is never regenerated from source and never read from the working tree. | Release pipeline | `[instruction]` |
| **I9** | **The gate is a whitelist.** Any manifest difference outside the enumerated compatible set fails the release when the major does not increase. An unrecognised difference kind fails; it does not pass by default. | Surface model | `[instruction]` |
| **I10** | **The manifest holds only comparable values.** An item whose value cannot be recorded as a stable ordinal string is absent from the manifest, and its compatibility is carried by release notes instead. | Surface model | `[instruction]` |
| **I11** | **Once recorded, an item leaves only by the removal rule.** An item present in a published manifest and absent from the candidate is a removal, whatever the reason for its absence. Making a surface unrecordable is not an exit from the gate after its first release. | Surface model | `[instruction]` |
| **I12** | **Every published manifest stays readable.** The manifest reader accepts every `manifestSchemaVersion` ever published, because baselines are immutable release assets and the gate must keep comparing against them for the product's lifespan. | Surface model | `[instruction]` |
| **I13** | **One publisher at a time.** At most one release-pipeline run publishes at any moment, held by a single CI concurrency group spanning every publishing workflow, queued rather than cancelled. | Release pipeline (CI configuration) | `[instruction]` — no `concurrency:` key exists in `.github/workflows/release.yml` or `.github/workflows/release-tag.yml` today |
| **I14** | **Only CI publishes.** No sink accepts a write from a developer machine. | Release pipeline | `[instruction]` |
| **I15** | **A push to `main` writes no version.** It moves `latest` and nothing else. No versioned image tag, tool package or module version is written outside a release (2026-09-20 decision). | Release pipeline | `[instruction]` — the main-push workflow has no path to a versioned sink |

### Configuration

| Id | Invariant | Owner | Enforcement |
|---|---|---|---|
| **I16** | **One project configuration file.** A project carries at most one configuration file. Two files differing only in format are an error, reported before anything is built. | Config | `[instruction]` |
| **I17** | **Keys and parameters are in bijection.** A configuration key exists if and only if a build parameter exists. Adding a parameter adds a key; removing a key requires removing the parameter, under the deprecation rules. | Config | `[instruction]` |
| **I18** | **Nothing is built on a configuration error.** Every configuration error found in one pass is reported together, and no build step runs. | Config | `[instruction]` |
| **I19** | **Precedence is total and attributed.** Every resolved value carries the tier it came from. No two tiers tie, and no value has an unknown source. | Config | `[instruction]` |
| **I20** | **Secrets are not project-file values.** A parameter declared secret is rejected when it appears in the project configuration file. It is supplied by argument, environment variable or mapping file only. | Config | `[instruction]` |
| **I21** | **Resolved configuration is never persisted.** The resolved set exists for the duration of one invocation. Any display of it redacts secret-declared values. | Config | `[instruction]` |
| **I22** | **Map-derived environment does not overwrite the process environment.** A value already present in the process environment wins over a value the mapping file produces. | Config, Build types | `[instruction]` — `forge/Common/Base.cs:209` loads the generated file into the process with the library's default overwrite behaviour, which the slice must pin explicitly |
| **I23** | **One validator.** The `node-template` flow resolves configuration by calling Config. No second validation implementation exists. | Config | `[instruction]` — `scripts/nuke/build.ps1` calls no configuration module today |

### Build and launch

| Id | Invariant | Owner | Enforcement |
|---|---|---|---|
| **I24** | **Generated environment files do not outlive the build.** Every environment file the build generates is removed on every exit path, success or failure. | Build types | **partial `[code]`** — `forge/Common/Base.cs:244-250` removes `.build/.build.env` from `OnBuildFinished`, which NUKE runs on both outcomes. The application environment file generated at `forge/Common/Components/INodeComponent.cs:48` into the repository root is **not** removed by any path; closing that is a slice obligation, not an existing property |
| **I25** | **No secret value reaches output or an image.** No secret-declared value appears on stdout, on stderr, in a log line, or in a layer of an image the product builds. | Build types, PowerShell module | partial `[code]` for the module (`PSModule.requirements.md` R-SEC-001, R-SEC-002); `[instruction]` elsewhere |
| **I26** | **Deprecation warns, never fails.** Use of a deprecated item emits a warning naming the item, the release that deprecated it and its replacement, and the invocation continues. | Config, Build types | `[instruction]` |
| **I27** | **A launcher never substitutes an image version.** When the configured image cannot be obtained the launcher fails. It never falls back to another version or to `latest`. | Launchers | `[instruction]` |
| **I28** | **A launcher does not reinterpret the build's outcome.** The global tool returns the container's exit status unchanged. The PowerShell module surfaces a non-zero status as a terminating error carrying that status (`PSModule.requirements.md` R-INVOKE-005). Neither maps one status onto another. | Launchers | partial `[code]` for the module (`scripts/powershell-module/Docker-BuildAgent.psm1:111`); `[instruction]` for the tool |
| **I29** | **The accepted build-type set is closed.** Exactly five build types are accepted: `docker`, `node`, `node-in-docker`, `node-template`, `forge`. Every entry point accepts the same five and no entry point defaults the type. | Build types | `[code]` — `scripts/nuke/build.ps1:4` (`ValidateSet`, `Position = 0, Mandatory`) and `PSModule.requirements.md` R-INVOKE-001 realised at `scripts/powershell-module/Docker-BuildAgent.psm1:111` |

### Container update

| Id | Invariant | Owner | Enforcement |
|---|---|---|---|
| **I30** | **One update per container at a time.** At most one update of a given target container name is in progress, enforced by a created-but-never-started container whose name derives deterministically from the target's name. | Updater | `[instruction]` |
| **I31** | **A lock is never taken over automatically.** Only the owning process, or an operator running the explicit lock-clearing command, removes a lock. Age alone never removes one. | Updater | `[instruction]` |
| **I32** | **The deadline is diagnostic, not an authorization.** A lock past its recorded deadline changes the message and nothing else. It does not permit the update to proceed and does not permit an automatic takeover. | Updater | `[instruction]` |
| **I33** | **The pull happens outside the lock.** The target image is obtained before the lock is created, so a slow pull never holds the lock. | Updater | `[instruction]` |
| **I34** | **The replacement differs from the target in image only.** Every other element of the container's configuration is reproduced exactly. Where the daemon cannot reproduce an element from inspect output, creation refuses and the update refuses; it never proceeds with an approximation. | Updater | `[instruction]` |
| **I35** | **Health is never evidence of fidelity.** The result of the health wait is never used to decide whether the replacement reproduces the target's configuration. Fidelity is decided solely by I34's creation refusal. | Updater | `[instruction]` |
| **I36** | **The log brackets every change.** The start entry is durably flushed before the first change to the target, and the outcome entry is written after the last. A log that cannot be written refuses the update before any change. | Updater | `[instruction]` |
| **I37** | **The lock outlives the outcome write.** The lock is released only after the outcome entry write has been attempted, so no other update starts while the record is still open. | Updater | `[instruction]` |
| **I38** | **Exactly one prior-image pin per target.** A target container has at most one prior-image pin at any time. Creating a new pin replaces the previous one, and the pin is removed only when the update reaches a terminal outcome. | Updater | `[instruction]` |
| **I39** | **Restore renames, it does not rebuild.** Restoring returns the retained prior container to its original name. It never recreates a container from inspect output, because that is the operation I34 already declares unsafe. | Updater | `[instruction]` |
| **I40** | **The update record holds no consumer secret.** No field of an update record carries the target container's environment, command or mounts. | Updater | `[instruction]` |
| **I41** | **A refusal changes nothing.** Every refusal before the first change to the target leaves no residue: no renamed container, no pin, no lock, and no open start entry. | Updater | `[instruction]` |
| **I42** | **Attribution comes from labels, not from names.** The lock, the pin and the prior container carry the target's full name in a label. Their own names carry only a truncated hash, which exists to be a valid unique identifier and is never the authority for which target they belong to. | Updater | `[instruction]` |
| **I43** | **An update restores the image and its container, nothing else.** Volumes, data, host configuration and consumer state are never restored. | Updater | `[instruction]` |
| **I44** | **Notification never changes the outcome.** A failed or slow notification does not change the exit status, and the webhook URL never appears in any output. | Updater, Notifications | `[instruction]` |

### Documentation and ownership

| Id | Invariant | Owner | Enforcement |
|---|---|---|---|
| **I45** | **Published documentation names nothing the product lacks.** The documentation site, the README and PowerShell help name no path, project, command, parameter, build type or discovery location absent from the tree or the manifest. | Docs check | `[instruction]` |
| **I46** | **One canonical contract per protected surface.** Each protected surface has exactly one canonical contract, and every other document naming that surface names its canonical source. `PSModule.requirements.md` is the PowerShell module's. | Docs check | `[instruction]` |
| **I47** | **The manifest is derived, never authored.** The candidate manifest is generated from declarations in the tree. It is never hand-edited and never committed as the baseline. | Surface model | `[instruction]` |

### Generated

This repository has not yet written `Invariant` unit records under `design/state/`
(`design/state-index.md`), so the table below — a **projected** marked region
(`AGENTS.shared.md` § *Marked regions*), rendered by `tools/Update-DesignProjection.ps1`
and overwritten on every regeneration — is empty for that reason, not because none of
the invariants above are real. It fills in as those records are written; nothing here
is written by hand.

<!-- invariants:start -->
| | Statement | Owner | Enforcement | Evidence |
|---|---|---|---|---|
<!-- invariants:end -->

---

## Types

### Release version

The product version. One value per release (I1), semantic, without build
metadata.

```csharp
namespace Release;

public sealed record ReleaseVersion(int Major, int Minor, int Patch, string? PreRelease)
{
    public static ReleaseVersion Parse(string value);
    public string ToTagString();      // "v2.0.0"
    public string ToPackageString();  // "2.0.0"
}
```

Semantics a declaration cannot carry: `ToTagString` produces the git tag and the
GitHub release tag; `ToPackageString` produces the image tag, the tool package version
and the module manifest version. The two forms differ by the `v` prefix and by nothing
else, so a change to either is a change to both.

### Release claim

The record that settles whether a version exists (I5). A GitHub draft release
bound to a commit SHA.

```csharp
namespace Release;

public enum ClaimState { Draft, Published }

public enum ReleaseSink
{
    ImageVersionedTag = 1,
    GlobalTool = 2,
    PowerShellModule = 3,
    ImageLatestTag = 4,
}

public sealed record ReleaseClaim(
    ReleaseVersion Version,
    string CommitSha,
    ClaimState State,
    string Notes,
    SurfaceManifest CandidateManifest);
```

Semantics: the enum's numeric values are the publication order and are load-bearing —
`ImageLatestTag` is last (I4). The claim carries no per-sink completion state;
what that costs, and what a resume may therefore assume, is `## Unresolved` U-6.

### Surface manifest

The mechanical compatibility record, published as a release asset.

```csharp
namespace Surface;

public enum SurfaceItemKind
{
    BuildType,
    BuildParameter,
    ConfigKey,
    ConfigSchemaVersion,
    ToolCommand,
    ToolParameter,
    ModuleCommand,
    ModuleParameter,
    TemplateLocation,
    ImageInvocation,
    ImageMountPoint,
    ImageEnvironmentInput,
}

public sealed record SurfaceItem(
    SurfaceItemKind Kind,
    string Name,
    string Value,
    string? DeprecatedSince,
    string? RemoveIn);

public sealed record SurfaceManifest(
    int ManifestSchemaVersion,
    string ProductVersion,
    IReadOnlyList<SurfaceItem> Items);

public enum SurfaceDifferenceKind
{
    ItemAdded,
    ItemRemoved,
    ValueChanged,
    DeprecationAdded,
    DeprecationRemoved,
    RemovalTargetChanged,
}

public sealed record SurfaceDifference(
    SurfaceDifferenceKind Kind,
    SurfaceItemKind ItemKind,
    string Name,
    string? BaselineValue,
    string? CandidateValue);

public sealed record SurfaceComparison(
    IReadOnlyList<SurfaceDifference> All,
    IReadOnlyList<SurfaceDifference> Blocking);
```

Semantics a declaration cannot carry:

- `(Kind, Name)` identifies an item. `Name` is a stable identifier, not a display
  string; renaming a `Name` is a removal plus an addition, which is how a rename
  becomes visible to the gate.
- `Value` is compared as an ordinal string. For a parameter it is the declared type
  and the declared default joined by `|`; a parameter with no default records the
  empty default, and acquiring one is therefore a `ValueChanged`. For a
  `TemplateLocation` it is the location's zero-based position, so reordering discovery
  is a change.
- The compatible set — the only differences that pass without a major increase — is
  `ItemAdded` and `DeprecationAdded`. Everything else is blocking (I9), including
  a `SurfaceDifferenceKind` a future reader does not recognise.
- `DeprecatedSince` and `RemoveIn` are `ToPackageString()` forms. `RemoveIn` is never
  below the next major.

### Project configuration and precedence

```csharp
namespace Config;

public enum ConfigurationTier
{
    InvocationArgument = 1,
    ModuleConfiguration = 2,
    ProcessEnvironment = 3,
    MappingFileEnvironment = 4,
    ProjectConfigurationFile = 5,
    DeclaredDefault = 6,
}

public sealed record ResolvedValue(
    string Key,
    string? Value,
    ConfigurationTier Tier,
    bool IsSecret);

public sealed record ResolvedConfiguration(
    IReadOnlyDictionary<string, ResolvedValue> Values)
{
    public override string ToString();  // secret values redacted (I21)
}
```

Semantics: the numeric values are the precedence order, lowest wins. Tier 3 above
tier 4 is what makes I22 true. `Value` is null only when a parameter is declared
nullable; a non-nullable parameter with no value at any tier is a
`ConfigErrorCode.RequiredValueMissing`, not a null.

### Container update

```csharp
namespace Update;

public enum UpdateOutcome
{
    Succeeded,
    AlreadyCurrent,
    RestoredAfterUnhealthy,
    UnhealthyNotRestored,
    RestoreFailed,
    Refused,
}

public sealed record UpdateOptions(
    TimeSpan HealthTimeout,
    bool RestoreOnFailure);

public sealed record UpdateRecord(
    int RecordSchemaVersion,
    Guid UpdateId,
    string ContainerName,
    DateTimeOffset StartedAt,
    string PriorImageId,
    string TargetImageReference,
    UpdateOptions Options,
    DateTimeOffset? CompletedAt,
    UpdateOutcome? Outcome,
    string? FailureCode);

public sealed record UpdateLock(
    Guid UpdateId,
    string ContainerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset Deadline,
    string OwnerHost,
    int OwnerProcessId);
```

Semantics a declaration cannot carry:

- `UpdateRecord` deliberately has no field for the target's environment, command or
  mounts (I40). Adding one is a contract change, not an implementation detail.
- `CompletedAt` and `Outcome` are null exactly while the record is open. An open
  record is residue (I41) — this is the field pair residue detection reads.
- `UpdateOptions` is recorded as given, before any defaulting, so a record says what
  the operator asked for.
- `Deadline` is written for the operator's benefit only (I32).
- `AlreadyCurrent` is a success, not a refusal: the target already runs the target
  image and the update takes no lock and writes no record.

### Prior image pin and prior container

These have no type. They are Docker objects, and their contract is their naming and
labelling:

| Object | Name | Labels |
| --- | --- | --- |
| Lock | `buildagent-lock-<h>` | `com.buildagent.role=lock`, `com.buildagent.container=<target>`, `com.buildagent.update-id=<guid>` |
| Prior container | `buildagent-prior-<h>` | `com.buildagent.role=prior`, `com.buildagent.container=<target>`, `com.buildagent.update-id=<guid>` |
| Prior image pin | tag `buildagent-prior:<h>` | — |

`<h>` is the first 32 lowercase hex characters of the SHA-256 of the target container
name in UTF-8. It makes the object name valid and unique; the
`com.buildagent.container` label is the authority for which target the object belongs
to (I42).

---

## Persisted schemas

### Surface manifest asset

A release asset named `surface-manifest.json`, JSON, UTF-8 without BOM, LF endings,
items sorted by `(Kind, Name)` ordinal so the file is byte-stable for a given surface.

```json
{
  "manifestSchemaVersion": 1,
  "productVersion": "2.0.0",
  "items": [
    {
      "kind": "BuildType",
      "name": "node-template",
      "value": "accepted",
      "deprecatedSince": null,
      "removeIn": null
    }
  ]
}
```

Migration: `manifestSchemaVersion` increments when the file's shape changes
incompatibly. Every version ever published stays readable forever (I12), because
baselines are immutable assets. A manifest whose version the reader does not know
fails the release; it is never treated as an empty baseline.

### Project configuration file

One file at the project root, base name `buildagent`, extension `.yml`, `.yaml` or
`.json`. Two or more present is `ConfigErrorCode.MultipleConfigurationFiles`
(I16); `.yml` and `.yaml` are two files, not one format with two spellings.

```yaml
schemaVersion: 1
buildType: node
parameters:
  artifacts-dir: artifacts
  notifications: false
```

- `schemaVersion` is a required integer. Its absence is
  `ConfigErrorCode.SchemaVersionMissing`; a value the reader does not support is
  `ConfigErrorCode.SchemaVersionUnsupported`. It increments only when the file's own
  shape changes incompatibly, never when a parameter is added or removed.
- `buildType` is required and is one of the five (I29).
- Every key under `parameters` corresponds to a build parameter of that build type
  (I17). An unknown key is `ConfigErrorCode.UnknownKey` and is fatal; it is never
  forwarded.
- Keys are the kebab-case CLI flag names without the `--` prefix, produced by the
  conversion the module already applies (`PSModule.requirements.md` R-INVOKE-003). A
  key has exactly one accepted spelling, and it is the spelling the manifest records.
- The JSON form is the same document. YAML-only constructs — anchors, multiple
  documents, non-string keys — are `ConfigErrorCode.MalformedDocument`.

Migration: a project file at a supported older `schemaVersion` is read under that
version's rules. There is no in-place rewriting and no silent upgrade.

### Update log

A per-user append-only file, one JSON object per line, flushed and synced before the
first change to the target (I36).

- Linux and macOS: `${XDG_STATE_HOME:-$HOME/.local/state}/docker-buildagent/updates.jsonl`
- Windows: `%LOCALAPPDATA%\Docker-BuildAgent\updates.jsonl`

Each line is an `UpdateRecord`. A start writes the record with `completedAt`,
`outcome` and `failureCode` null; the terminal write appends a second line with the
same `updateId` and those fields populated. The later line for an `updateId` wins.

Migration: `recordSchemaVersion` is per record, not per file, so one file mixes
versions. A record at an unknown version is still read for `updateId`,
`containerName` and the presence of `outcome`, which is all residue detection needs. A
line that cannot be parsed at all is treated as an open record for an unknown
container and refuses every update on that host until an operator clears it — the log
is evidence that something changed, and an unreadable line is evidence that cannot be
dismissed.

---

## Public surface

### `build <type>` inside the image

Declared at [`scripts/nuke/bin/build`](../scripts/nuke/bin/build) and
[`scripts/nuke/build.ps1`](../scripts/nuke/build.ps1).

Semantics the declarations cannot carry:

- `-type` is positional and mandatory and never acquires a default. A default would
  run a build the caller did not name.
- `-artifactsDir` defaults to `/nuke/forge`, an image-internal path. That default is a
  manifest item: changing it is a documented-behaviour change on a protected surface.
- `-nodeTemplateRepositoryUrl` accepts `<url>#<branch>`. The `#` separator is part of
  the surface.
- Remaining arguments pass to the underlying build verbatim, in order. The wrapper
  neither reorders nor de-duplicates them.
- The wrapper propagates the inner exit status and substitutes `1` only when no status
  is available (`scripts/nuke/bin/build:11-15`).

Exit statuses — documented behaviour, not manifest items:

| Status | Meaning |
| --- | --- |
| 0 | The build succeeded |
| 2 | Configuration is invalid; nothing was built (I18) |
| 3 | A required discovery target was not found |
| 4 | An external template could not be fetched |
| 5 | The Docker daemon was unavailable or rejected the request |
| 6 | A registry operation failed |
| 1 | Any other failure |

### Image invocation

Declared at [`Dockerfile`](../Dockerfile).

Semantics the declaration cannot carry:

- The image declares **no `ENTRYPOINT`**. The invocation surface is
  `docker run <image> build <type> [args]`, resting on `CMD ["pwsh"]` being overridden
  by the command the caller supplies and on `build` being on `PATH` from
  `/usr/local/bin/`. The manifest records the effective invocation, not an
  `ENTRYPOINT` instruction that does not exist.
- `/workspace` is the mount point and the working directory. It is a manifest item.
- `EXPOSE 3000` and the `HEALTHCHECK` belong to the documentation server this image
  can run. They are not the health check the Updater waits on — that is the *target*
  container's own declared check.
- The build argument `IMAGE_VERSION` carries the value from I1; the image never
  defaults it at release time.

### Global tool

Greenfield. The tool package identifier and the invoked command name are
`## Unresolved` U-1; the command surface below is determined regardless of the name.

```text
<tool> build <type> [--<parameter> <value>]...
<tool> update <container> [--image <reference>] [--health-timeout <duration>]
                          [--no-restore] [--notify <url>]
<tool> update --clear-lock <container>
```

- `build` accepts the same five types and the same parameter names as the in-image
  command (I29). It launches the versioned image matching the tool's own version
  and never substitutes another (I27).
- `update` defaults `--image` to the target container's image reference resolved
  afresh against the registry, and `--health-timeout` to 120 seconds.
- `--no-restore` disables automatic restore; restore is the default.
- `--clear-lock` is the only operator action that removes a lock it does not own
  (I31). It takes no other action and never touches the target.

Exit statuses:

| Status | Outcome |
| --- | --- |
| 0 | `Succeeded` or `AlreadyCurrent` |
| 10 | `RestoredAfterUnhealthy` |
| 11 | `UnhealthyNotRestored` |
| 12 | `RestoreFailed` |
| 20 | Refused — the target is absent or its shape is unsupported |
| 21 | Refused — a lock is held |
| 22 | Refused — residue from an earlier update is present |
| 23 | Refused — the prior image cannot be kept |
| 24 | Refused — the update log cannot be written |
| 30 | The target image could not be obtained |
| 1 | Any other failure |

Every refusal message names the target container, the reason, and the operator action
that clears it. A lock refusal additionally names the lock's owning host, process and
age, and says whether the deadline has passed and that the deadline does not authorise
a takeover (I32).

**Verification reaches Linux only.** No Windows host running Docker Desktop with Linux
containers is available as a runner, and GitHub-hosted Windows runners cannot run Linux
containers (2026-09-20 decision). Every status above is verified automatically on
Linux. On Windows only what needs no live daemon — argument translation, Docker host
resolution, path and mount handling — is covered. Live-daemon update behaviour on a
Windows host is a stated, unverified gap, and no document may describe it as verified.

### PowerShell module

Canonical contract: [`PSModule.requirements.md`](../PSModule.requirements.md).
Declared at
[`scripts/powershell-module/Docker-BuildAgent.psd1`](../scripts/powershell-module/Docker-BuildAgent.psd1)
and
[`scripts/powershell-module/Docker-BuildAgent.psm1`](../scripts/powershell-module/Docker-BuildAgent.psm1).

Semantics this document adds:

- The exported set — `Set-BuildAgentConfig`, `Invoke-Build`, `BuildAgentConfig` — is a
  protected surface; each exported name and each parameter is a manifest item.
- `BuildAgentConfig.Parameters` is precedence tier 2 and `-args` is tier 1; that is
  R-INVOKE-002 expressed as `ConfigurationTier`.
- The module's default `DockerImage` is the module's own version, not `latest`
  (2026-09-20 decision). This amends R-CONFIG-002 and is a breaking change in 2.0.0
  that the migration guide carries. The reference stays overridable, and an override is
  used verbatim.
- The module is tested on Windows PowerShell 5.1 and PowerShell 7, and both are
  release gates rather than best effort.

### Docker template discovery

Declared at
[`forge/Docker/Docker.cs`](../forge/Docker/Docker.cs) and
[`forge/Common/Utilities/Docker.cs`](../forge/Common/Utilities/Docker.cs).

Semantics this document adds: the ordered discovery locations are a protected surface.
Each location is a `TemplateLocation` manifest item whose `Value` is its position, so
adding a location at the end is compatible and inserting one anywhere else is not.
Today's order is the explicit templates directory when it exists as a directory, then
that same value resolved under the root directory; a Dockerfile is taken from
`<templates>/Dockerfile.<appType>` when no Dockerfile exists at the configured path.

### Build parameters

Declared at [`forge/Common/Parameters/`](../forge/Common/Parameters/) —
`ForgeParams`, `DockerParams`, `NodeParams`, `NodeInDockerParams`.

Semantics this document adds: every public property of a `*Params` class is a
`BuildParameter` manifest item and a configuration key (I17), derived by the
existing extractor (`PSModule.requirements.md` R-EXTRACT-001 to R-EXTRACT-005).
`RegistryToken`, `RegistryUser` and `NotificationsWebHookUrl` are secret-declared:
rejected in the project configuration file (I20) and redacted in every display
(I21). The classes carry no nullability annotations today; the slice that lands
Config annotates them, and an annotation that changes a parameter's declared type is a
`ValueChanged` the gate will see.

---

## Error semantics

Every module raises exactly one enumerated error type. No bare exception and no string
error crosses a module boundary. Retryable means an unchanged retry may succeed
without operator action.

### Config — `ConfigError(ConfigErrorCode Code, string? File, string? Key, string Message)`

| Code | Raised when | Retryable | Caller does |
| --- | --- | --- | --- |
| `MultipleConfigurationFiles` | More than one `buildagent.*` file is at the project root | No | Report every path found; exit 2 |
| `FileUnreadable` | The file exists but cannot be opened | Yes | Report the path; exit 2 |
| `MalformedDocument` | The file is not well-formed, or uses a construct with no JSON equivalent | No | Report the path and position; exit 2 |
| `SchemaVersionMissing` | `schemaVersion` is absent | No | Report the supported versions; exit 2 |
| `SchemaVersionUnsupported` | `schemaVersion` is not a supported value | No | Report the supported versions; exit 2 |
| `UnknownBuildType` | `buildType` is absent or outside the five | No | Report the accepted five; exit 2 |
| `UnknownKey` | A key under `parameters` has no build parameter | No | Report the key and the nearest known key; exit 2 |
| `SecretKeyRejected` | A secret-declared parameter appears in the file | No | Report the key and the tiers that may supply it; exit 2 |
| `ValueTypeMismatch` | A value cannot convert to the parameter's declared type | No | Report key, declared type and the value's shape, never the value (I25); exit 2 |
| `RequiredValueMissing` | A non-nullable parameter has no value at any tier | No | Report the key and the tiers consulted; exit 2 |

All Config errors are accumulated and reported in one pass (I18).

### Surface model — `SurfaceError(SurfaceErrorCode Code, string Message)`

| Code | Raised when | Retryable | Caller does |
| --- | --- | --- | --- |
| `BaselineMissing` | The baseline release has no manifest asset | No | Fail the release; a human decides whether this is the first gated release |
| `BaselineUnreadable` | The asset cannot be downloaded or parsed | Yes | Fail the release |
| `ManifestSchemaUnsupported` | A manifest's `manifestSchemaVersion` is unknown to the reader | No | Fail the release (I12); never treat as empty |
| `DerivationFailed` | The candidate manifest cannot be derived from the tree | No | Fail the release |
| `DuplicateItem` | Two items share `(Kind, Name)` | No | Fail the release |
| `BlockingDifference` | A difference outside the compatible set with no major increase | No | Fail the release, listing every blocking difference (I9) |

### Release pipeline — `ReleaseError(ReleaseErrorCode Code, ReleaseSink? Sink, string Message)`

| Code | Raised when | Retryable | Caller does |
| --- | --- | --- | --- |
| `VersionAlreadyExists` | A claim, image tag or git tag holds the version | No | Fail before any write (I5) |
| `MajorBelowCurrent` | The candidate major is below the highest published | No | Fail before any write |
| `TagPointsElsewhere` | A git tag for the version exists on another commit | No | Fail; a human resolves it |
| `NotesSectionMissing` | Notes lack breaking-changes or deprecations | No | Fail before any write (I7) |
| `SurfaceGateFailed` | The comparison returned blocking differences | No | Fail before any write |
| `ClaimCreationFailed` | The draft release cannot be created | Yes | Fail; nothing was written |
| `SinkPublishFailed` | A sink rejected the write | Yes | Fail naming `Sink`; leave the claim open for a resume |
| `NotPublishedByCi` | The run is not the CI publishing context | No | Fail (I14) |

`SinkPublishFailed` is the only error that leaves state behind. What a resume may
conclude from an existing sink artifact is `## Unresolved` U-6.

### Updater — `UpdateError(UpdateErrorCode Code, string ContainerName, string Message)`

| Code | Raised when | Retryable | Caller does |
| --- | --- | --- | --- |
| `TargetNotFound` | No container has that name | No | Exit 20 |
| `TargetAutoRemove` | The target is `--rm` | No | Exit 20; nothing can be preserved |
| `TargetOrchestratorManaged` | The target carries orchestrator ownership labels | No | Exit 20; the orchestrator owns updates |
| `TargetShapeUnsupported` | The configuration contains an element that cannot be reproduced | No | Exit 20 naming the element (I34) |
| `ImageUnavailable` | The target image cannot be obtained | Yes | Exit 30; no lock was taken (I33) |
| `LockHeld` | A lock exists for the target | Yes | Exit 21 naming owner, process and age (I32) |
| `ResiduePresent` | A prior container or an open record exists | No | Exit 22 naming the object and the clearing command |
| `LogUnwritable` | The start entry cannot be flushed | Yes | Exit 24 before any change (I36) |
| `PinFailed` | The prior image cannot be pinned | Yes | Exit 23 before any change |
| `ReplacementCreateFailed` | The daemon rejected creation | No | Restore, then exit per outcome |
| `HealthCheckAbsent` | The target declares no health check | No | Treat as unhealthy; restore per `RestoreOnFailure` |
| `HealthTimedOut` | The check did not pass within the timeout | Yes | Restore per `RestoreOnFailure`; exit 10 or 11 |
| `ReplacementExited` | The replacement exited before becoming healthy | No | Restore per `RestoreOnFailure`; exit 10 or 11 |
| `RestoreFailed` | The prior container cannot be returned to its name | No | Exit 12 naming the prior container and its labels |

`LockHeld` is retryable in the sense that the same invocation may succeed later. It
never becomes a takeover (I31).

### Launchers — `LauncherError(LauncherErrorCode Code, string Message)`

| Code | Raised when | Retryable | Caller does |
| --- | --- | --- | --- |
| `DockerUnavailable` | The daemon cannot be reached | Yes | Exit 5 |
| `ImageUnavailable` | The configured image cannot be obtained | Yes | Exit 5; never substitute a version (I27) |
| `WorkspaceInvalid` | The workspace path is absent or not a directory | No | Exit 3 |
| `BuildFailed` | The container exited non-zero | No | Propagate the status unchanged (I28) |

### Docs check — `DocsCheckError(DocsCheckErrorCode Code, string Document, string Name, string Message)`

| Code | Raised when | Retryable | Caller does |
| --- | --- | --- | --- |
| `UnknownName` | A document names a path, command, parameter, build type or location the tree and manifest lack | No | Fail the PR check naming document and name (I45) |
| `CanonicalSourceMissing` | A document covering a protected surface names no canonical contract | No | Fail the PR check (I46) |
| `CanonicalSourceConflict` | Two documents claim to be canonical for one surface | No | Fail the PR check |

### Notifications — `NotificationError(NotificationErrorCode Code, string Message)`

| Code | Raised when | Retryable | Caller does |
| --- | --- | --- | --- |
| `DeliveryFailed` | The webhook did not accept the payload | Yes | Warn without the URL; do not change the exit status (I44) |
| `NotConfigured` | Notification was requested with no URL | No | Warn; do not change the exit status |

---

## Design state

This section is the contract side of the design-state mechanism
(`AGENTS.shared.md` § *Design state*). `tools/Test-DesignState.ps1` parses the two
tables below and compares them against its own hard-coded lists. That restatement is
deliberate and is the one sanctioned exception to *Single ownership*: the comparison
has no meaning unless both sides are written independently. A table here that the
checker cannot read is `ContractListUnreadable` — the class it feeds is then
**uncomputed, never clean**.

### Artifacts of a unit kind

Which files in this checkout a unit record of each kind is expected to exist for.
`GlobDisagreement` compares the file set this table resolves to against the set the
checker enumerates — never the pattern text. A pattern is repository-relative and
wildcards its final segment only. An exclusion carrying a wildcard is matched against
the basename; one without is an exact repository-relative path.

| Kind | Glob | Excluded |
|---|---|---|
| command | `.claude/commands/*.md` | `*-local.md` |
| script | `tools/*.ps1` | `*.Tests.ps1` |
| document | `design/*.md`, `templates/design/*.md`, `*.md`, `.claude/COMPANIONS.md`, `.github/ISSUE_TEMPLATE/*.md`, `codex/PROFILES.md` | `design/FROZEN.md`, `CLAUDE.md` |
| invariant | not a tree path | — |

The `invariant` row carries no pattern in either cell and drops out of the parse for
that reason, not by being named. Its unit set is § *Invariants* above, read as rows of
the form `| **I<n>** | … |` — which is why the invariants are stated as tables with a
sequential `I<n>` id rather than as prose with a domain-qualified one. An invariant
added outside that row shape is not in the unit set, and its absence is reported by
nothing at all.

There are no `component` artifacts in this repository, and the checker enumerates no
kind by that name.

### The divergence classes

What the checker may report, and what each report obliges the caller to do. Exit 0 is
clean, exit 1 is findings, exit 2 is could-not-evaluate. Exit 2 outranks exit 1.

**Blocking.** A finding of any class below fails the check.

| Class | Raised when | Caller sees |
|---|---|---|
| `UnresolvedId` | A record field names an id that has no record | The naming field and the missing id |
| `AnchorMissing` | A record's anchor names a path that does not exist in the tree | The pointer field and its value |
| `OwnerMismatch` | A surface's declared owner is not among the units exposing it | The declared owner and the units that expose it |
| `UnrecordedArtifact` | A file matched by § *Artifacts of a unit kind*, or an invariant in § *Invariants*, has no record | The artifact and the kind it was expected to be recorded as |
| `ProjectionStale` | A projected marked region does not match what the projector renders | The region and the file holding it |
| `RegionMalformed` | A marked region's start and end markers are missing, unpaired or crossed | The malformed region |
| `IdCollision` | Two records claim the same id | The id and both records |
| `DecisionAnchorAmbiguous` | A decision's anchor resolves to more than one place | The decision and the candidates |
| `LogEntryUnrecorded` | A decision log entry has no decision record behind it | The unrecorded entry |
| `EnforcementUnevidenced` | An invariant claims an enforcement its evidence does not support | The invariant and the claimed enforcement |
| `ClosureOverBudget` | A closure exceeds the 16,384-byte ceiling | The byte count, the ceiling and the largest contributor |
| `ClassListDisagreement` | This section and the checker's own class lists differ | The class and which side holds it |
| `GlobDisagreement` | § *Artifacts of a unit kind* and the checker's enumerators resolve to different file sets | The kind and the files one side has that the other does not |

**Reported, never blocking.** These record a divergence the check is not entitled to
fail on, because the authority they compare against is outside the checkout.

| Class | Raised when | Why it never blocks |
|---|---|---|
| `MirrorStale` | A `WorkRef` mirror is older than the commit it was taken at | GitHub is the authority; a stale mirror is expected in a checkout with no network |
| `WorkStateDivergence` | A mirrored work item disagrees with its record | Same authority; the mirror is not evidence of work state |
| `PinAncestry` | A pin does not sit on the ancestry it claims | The claim may be true and the checkout shallow |
| `SemanticDisagreement` | A record's prose contradicts what it points at | No mechanical check decides this; a reading raises it |

**Could not evaluate.** Not findings. Each names a check the run could not perform, so
the classes it feeds are uncomputed. Any entry here makes the run exit 2, and a run
that exits 2 has not established that the corpus is clean.

| `DesignStateFailure` | Raised when | Caller does |
|---|---|---|
| `StateSetAbsent` | `design/state/` does not exist | Treat every class as uncomputed |
| `RecordUnparseable` | A record cannot be read as a record | Fix the record; do not read its absence as clean |
| `TrackerUnavailable` | The tracker could not be reached | Treat mirror-derived classes as uncomputed |
| `ShallowCheckout` | History needed for ancestry is not present | Treat `PinAncestry` as uncomputed |
| `ProjectorFailed` | The projector did not run to completion | Treat `ProjectionStale` as uncomputed |
| `ContractListUnreadable` | A table this section owns could not be parsed | Treat the class it feeds as uncomputed |

### The freeze

`design/FROZEN.md` downgrades every blocking class to reported for the duration of the
freeze: the classes are still computed and still reported, and the run no longer exits
1 on them. It does not touch exit 2 — a could-not-evaluate entry fails a frozen run
exactly as it fails an unfrozen one, because a freeze suspends a judgement, not the
ability to make one.

A freeze file states `Frozen because:` and `Lifts when:`, and the run reproduces both
verbatim alongside the count of records in scope, so the report says on whose authority
the downgrade happened and what ends it.

---

## Unresolved

Each entry names what it blocks. No entry here may be resolved by an implementing
slice inventing an answer.

Ids are permanent. An entry resolved by a decision is struck from this list and never
reused, so the numbering carries gaps.

**U-1 — The global tool's package identifier and command name.** The design fixes the
tool's responsibilities and not its identity, and `10-design.md` Open question 1 leaves
ownership of the .NET tool feed and the PowerShell Gallery, and custody of their
publishing keys, open.
*Blocks:* the tool's `ToolCommand` manifest items, its published invocation in every
document, the CI step that publishes it, and the CI step that publishes the PowerShell
module to the Gallery.

*U-2, U-3 and U-4 were resolved by the 2026-09-20 decisions and are struck.*

**U-5 — How the `node-template` flow reaches Config across the language boundary.** The
design requires one validator (I23) and does not determine whether PowerShell
calls Config in-process, through a tool subcommand, or through a JSON-emitting
invocation. Each is a different public surface.
*Blocks:* Config's public surface, I23's enforcement, and the `node-template`
slice.

**U-6 — The claim carries no per-sink artifact identity.** Red-team finding F4,
unadjudicated. Nothing in a claim proves an existing sink artifact was produced by that
claim, and resume safety depends on exactly that.
*Blocks:* the resume rule after `SinkPublishFailed`, and the strength of I3 and
I5.

**U-7 — Claim checking and claim creation are not atomic.** Red-team finding F5,
unadjudicated. The design permits two drafts in the window between the check and the
creation, so the claim is not by itself a concurrency backstop.
*Blocks:* I13's sufficiency, and whether a second backstop is needed.

**U-8 — The global tool hosts the Updater, which depends on Notifications.** Red-team
finding F6, unadjudicated. The tool is declared to share no Build-types code, yet
Notifications is declared to live within Build types.
*Blocks:* the Updater's module boundary and the tool's package references.

**U-9 — `latest` moves before the release is published.** Red-team finding F8,
unadjudicated. `10-design.md` § Control flow 2 moves `latest` at step 7, before the
fallible step 8, which contradicts I4.
*Blocks:* I4, and the `ReleaseSink` ordering that encodes it.

**U-10 — The enumerated set of unsupported container shapes.** `10-design.md` states
the rule and delegates the list here, but the list cannot be written from the design:
it is whatever the Docker API fails to reproduce from inspect output, which is a
verified fact about the daemon rather than a design choice. Anonymous volumes and
legacy container links are in it on the design's own statement; the rest needs a
round-trip probe per shape.
*Blocks:* `UpdateErrorCode.TargetShapeUnsupported`'s refusal list and the refusal tests
the brief requires.

*U-11 was resolved by the 2026-09-20 decision on Windows verification and is struck.
The gap it names is now stated under § Global tool rather than pending.*

*U-12 named the invariants being unparseable by the design-state reader and is struck.
It was resolved on 2026-09-20 by restating § Invariants as tables keyed on a sequential
`I<n>` id, which the reader matches; the former domain-qualified ids (`I-REL-n`,
`I-CFG-n`, `I-BLD-n`, `I-UPD-n`, `I-DOC-n`, `I-SURF-1`) are retired and never reused,
and the domain is now carried by the subsection heading alone.*
