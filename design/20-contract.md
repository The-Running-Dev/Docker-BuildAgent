# Contract — Docker-BuildAgent

Input: [`10-design.md`](10-design.md). That document carries intent; this one carries the
semantics an implementing agent is checked against. Names, config keys, exit-code values and
message rules live here and nowhere else.

**Semantics, not shape.** Where the tree already declares something, this document points at the
declaration and states only what the declaration cannot. Where the code does not exist yet, a
declaration appears here as a **scaffold** — signatures only, no bodies — and the slice that
materialises it replaces the scaffold with a pointer in the same commit. That replacement is
descriptive drift corrected where found, not a contract amendment.

`PSModule.requirements.md` remains the canonical contract for the PowerShell module surface
(`AGENTS.md`, *Repository identity*). This document does not restate it; it states only the
module obligations that arise from *this* design and are absent there.

---

## Invariants

Each is written so it could become an assertion. **Enforced by code** means a reader may trust it
without checking; **enforced by instruction** means only a human or a review step holds it.

### Release

| # | Invariant | Owner | Enforced by |
|---|---|---|---|
| I1 | At most one release claim exists for a version at any time. | Release pipeline | Code — one queued CI concurrency group across every publishing workflow, with the claim check as backstop |
| I2 | No sink is written before the claim exists. | Release pipeline | Code — fixed step order; steps 1–5 are read-only |
| I3 | The image versioned tag, the global tool package and the PowerShell module of one release carry byte-identical version strings. | Release pipeline | Code — the version is computed once and stamped into all three |
| I4 | A version tag published from 2.0.0 onward is never overwritten, deleted or expired. | Release pipeline | Code for the refusal path (the claim check refuses a re-publish); **instruction** for deletion — nothing in the product can delete a published tag, and no product code may acquire that ability |
| I5 | `latest` moves only after every versioned sink holds the release. | Release pipeline | Code — `latest` is last in the fixed publish order |
| I6 | Every surface manifest is derived from declarations at release time, never hand-written and never read from the tree. | Surface model | Code |
| I7 | Every manifest item carries a comparable value. An item whose value cannot be recorded is absent from the manifest. | Surface model | Code — derivation drops what it cannot value, and the drop is reported |
| I8 | A release fails before the claim on any manifest difference from its baseline that is not in the compatible set, unless the major increases. | Release pipeline | Code |
| I9 | A release fails before the claim if either required release-notes section heading is absent. | Release pipeline | Code |
| I10 | A release's major is never lower than the highest published major. | Release pipeline | Code |
| I11 | A push to `main` writes no version to any sink. It moves `latest` only. | Release pipeline | Code — the main-push workflow has no path to a versioned sink |

### Configuration

| # | Invariant | Owner | Enforced by |
|---|---|---|---|
| I12 | At most one project configuration file exists for a project. Two or more is a validation failure, never a precedence decision. | Config | Code |
| I13 | Every validation error found in one resolution pass is reported together, before any build step runs. | Config | Code |
| I14 | The resolved configuration is never written to disk. | Config | Code |
| I15 | A parameter marked secret is rejected in the project file, and never appears unredacted in any output stream. | Config | Code — rejection at validation; redaction over **every** secret-marked parameter, not only token-shaped patterns |
| I16 | Configuration precedence holds exactly as ordered in *Resolution order*, and every resolved value records the tier it came from. | Config | Code |
| I17 | A project configuration key exists if and only if a build parameter of that name exists. | Config, Surface model | Code — keys are projected from the same declarations the manifest reads |
| I18 | Map-derived environment never overwrites a variable already set in the process environment. | Config | Code |
| I19 | Generated environment files are removed on every exit path, including failure and signal. | Build types | Code |

### Container update

| # | Invariant | Owner | Enforced by |
|---|---|---|---|
| I20 | At most one update of a given container **name** is in its critical section at any time, across every client of the daemon. | Updater | Code — daemon-side name uniqueness on the lock container |
| I21 | A lock is never removed by any process other than the one that created it, except by the explicit operator command. A lock past its deadline is reported, never taken over. | Updater | Code |
| I22 | The registry pull completes before the lock is acquired. | Updater | Code — step order |
| I23 | The replacement container differs from the target in image only. | Updater | Code — by refusal at `RefuseUnfaithfulCreate` before anything changes, **not** by any check afterwards; health is not evidence of configuration fidelity |
| I24 | Nothing about the target changes before the start log entry is written and flushed. | Updater | Code |
| I25 | Exactly one prior image pin exists per target container. | Updater | Code |
| I26 | Restore renames the retained prior container back. It never re-creates a container from inspect output. | Updater | Code |
| I27 | The lock is released only after the outcome entry is written, or after that write has failed and been reported. | Updater | Code |
| I28 | An update record contains no container environment, command or mount data. | Updater | Code — the record type has no field to hold it |
| I29 | An interrupted update is never reclaimed automatically. The next update refuses on the residue and names the operator action. | Updater | Code |

### Documentation

| # | Invariant | Owner | Enforced by |
|---|---|---|---|
| I30 | Published documentation, README and module help name no command, parameter, build type, discovery location, repository path or linked tree file absent from the manifest or the tree. | Docs check | Code — PR gate. Behavioural claims are **instruction** only, and the gate does not claim otherwise |

---

## Types

### Carried by the tree — pointers

| Entity | Declared in | What the declaration cannot say |
|---|---|---|
| Build parameters per type | [`Forge/Common/Parameters/ForgeParams.cs`](../Forge/Common/Parameters/ForgeParams.cs), [`DockerParams.cs`](../Forge/Common/Parameters/DockerParams.cs), [`NodeParams.cs`](../Forge/Common/Parameters/NodeParams.cs), [`NodeInDockerParams.cs`](../Forge/Common/Parameters/NodeInDockerParams.cs), [`NotificationParams.cs`](../Forge/Common/Parameters/NotificationParams.cs) | These classes are the **sole source** of the parameter vocabulary. The manifest and the project-file key set are both projected from them (I17), so a property added here becomes public surface on the next release whether or not anyone intended it. A property that must not become public surface does not belong in these classes. `NotificationParams.NotificationsWebHookUrl` is secret-marked: rejected in the project file, redacted in all output (I15). |
| Build-time paths and generated file names | [`Forge/Common/Entities/BuildConfig.cs`](../Forge/Common/Entities/BuildConfig.cs) | `EnvFile`, `EnvMapFile`, `AppEnvFile` and `AppEnvMapFile` are build **outputs**, not configuration sources. `AppEnvMapFile` generates the built app's environment from resolved configuration and is therefore outside the precedence order entirely. |
| Build version information | [`Forge/Common/Entities/VersionInfo.cs`](../Forge/Common/Entities/VersionInfo.cs) | This is the build's view of a version and is **not** the release version. It carries no major-ordering constraint and no claim binding. The release version is a distinct type below. |
| Docker-template discovery order | [`Forge/Common/Services/DockerService.cs`](../Forge/Common/Services/DockerService.cs) | The **order** is a protected surface and a manifest item. The list may be appended to in a minor release and reordered only in a major. On discovery failure every location searched is reported, in order. |
| PowerShell module surface | [`PSModule.requirements.md`](../PSModule.requirements.md) | Canonical, with one change from 2.0.0: the module's default image reference is the module's **own version**, not `latest` (2026-09-20 decision). This amends `R-CONFIG-002` and is a breaking change the migration guide carries. |

### Scaffolds — no code yet

Written in the project's C# with nullable reference types, matching
[`VersionInfo.cs`](../Forge/Common/Entities/VersionInfo.cs). Each is replaced by a pointer when its
slice lands.

```csharp
namespace Entities;

// Release version. Distinct from VersionInfo.
public sealed record ReleaseVersion
{
    public required string Value { get; init; }          // SemVer 2, no leading "v"
    public required int Major { get; init; }
    public string TagName => "v" + Value;
}

public enum ClaimState { Claimed, Published }

public sealed record ReleaseClaim
{
    public required ReleaseVersion Version { get; init; }
    public required string CommitSha { get; init; }
    public required ClaimState State { get; init; }
    public required string ReleaseNotes { get; init; }
    public required SurfaceManifest Manifest { get; init; }
}

public sealed record SurfaceItem
{
    public required string Name { get; init; }           // stable identity across releases
    public required string Value { get; init; }          // comparable; never null — an item
                                                         // without a value is not in the manifest
    public string? DeprecatedInVersion { get; init; }    // null means not deprecated
}

public sealed record SurfaceManifest
{
    public required ReleaseVersion Version { get; init; }
    public required IReadOnlyList<SurfaceItem> Items { get; init; }
}

public enum SurfaceDifferenceKind { Added, Removed, ValueChanged, DeprecationAdded, Reordered }

public sealed record SurfaceDifference
{
    public required string ItemName { get; init; }
    public required SurfaceDifferenceKind Kind { get; init; }
    public string? BaselineValue { get; init; }
    public string? CandidateValue { get; init; }
}

public enum ConfigurationTier
{
    InvocationArgument = 1,
    ModuleConfiguration = 2,
    ProcessEnvironment = 3,
    MapDerivedEnvironment = 4,
    ProjectFile = 5,
    DeclaredDefault = 6,
}

public sealed record ResolvedValue
{
    public required string Key { get; init; }
    public required string? Value { get; init; }
    public required ConfigurationTier Source { get; init; }
    public required bool IsSecret { get; init; }
}

public enum UpdateOutcome
{
    Succeeded, NoOp, UnhealthyRestored, UnhealthyNotRestored, RestoreFailed, Refused,
}

public sealed record UpdateRecord
{
    public required string UpdateId { get; init; }
    public required string ContainerName { get; init; }
    public required string ContainerId { get; init; }
    public required string PriorImageId { get; init; }
    public required string RequestedImageReference { get; init; }
    public string? NewImageId { get; init; }
    public required string PriorContainerName { get; init; }
    public required int HealthTimeoutSeconds { get; init; }
    public required bool RestoreEnabled { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public UpdateOutcome? Outcome { get; init; }
    // No field for environment, command or mounts. I28 is held by the type, not by a caller
    // remembering to omit them.
}
```

**The update lock has no code representation of its own** — it is a created-but-never-started
container at the daemon. Its constraints instead:

- Its name is `buildagent-lock-<h>`, where `<h>` is the lowercase hex of the first 16 bytes of
  SHA-256 over the **target container's name** in UTF-8. Derivation from the name, not the id, is
  load-bearing: the id behind that name changes at rename time, inside the section the lock guards.
- Its image is the target's current image id, which is always present locally. **Taking the lock
  never pulls.**
- Its labels carry `dev.buildagent.role=lock`, `.target-name`, `.update-id`, `.owner-machine`,
  `.owner-process`, `.acquired-at` (ISO 8601), `.deadline` (ISO 8601).
- `.deadline` is **diagnostic, never an authorization.** It may be read to word a refusal. It may
  never be compared in order to decide that acting is permitted. Clock skew must be able to change
  only message text, never whether two processes mutate one container.

---

## Persisted schemas

### Project configuration file

- **Location:** project root. **Base name:** `buildagent`. **Extensions:** `.yaml`, `.yml`, `.json`.
- **More than one match is a validation failure** (I12), not a precedence decision, and the error
  names every file found.
- **Required key:** `schemaVersion` (string). A file without it, or with a version outside the
  release's supported set, fails validation.
- **Remaining keys:** the build parameter vocabulary, camelCase, one key per parameter (I17).
  Unknown keys fail. Secret-marked keys fail (I15).
- **Schema version is independent of the product version.** It is bumped only when the file's own
  shape changes incompatibly. The supported set is itself a manifest item, and adding to it is a
  compatible difference.
- **Migration: none, deliberately.** The file is consumer-owned and read-only to the product. The
  product never rewrites a consumer's file to a newer schema; a consumer migrates by editing,
  guided by the migration guide. Silent rewriting is forbidden — it would edit a consuming
  repository, which the brief's non-goals rule out.

### Update log

- **Location:** per-user state on the machine running the command.
  - Linux/macOS: `$XDG_STATE_HOME/docker-buildagent/updates.jsonl`, defaulting to
    `~/.local/state/docker-buildagent/updates.jsonl`.
  - Windows: `%LOCALAPPDATA%\docker-buildagent\updates.jsonl`.
- **Format:** JSON Lines, append-only, one serialised `UpdateRecord` per line.
- **Write discipline:** the start entry is written **and flushed** before the first change (I24);
  the outcome entry after the last. A start line with no matching outcome line is the durable
  evidence of an interruption.
- **Key:** `UpdateId`. Entries are never rewritten or removed by the product; a start line is
  resolved by appending its outcome, not by editing it.
- **Not a lock and not shared state.** It is per-user and per-machine, so another client of the
  same daemon cannot read it. Residue detection therefore keys on the **lock**, never on this log.
- **Migration: none.** An unparseable line is skipped with a warning and never blocks an update —
  the log is evidence, not control flow.

### Prior image pin

- **Tag:** `buildagent-prior:<h>`, same `<h>` derivation as the lock.
- Exactly one pin per target container (I25). A successful update replaces it.
- Its only purpose is to keep the prior image from being pruned once the prior container is gone.

### Prior container

- **Name:** `buildagent-prior-<h>`, same `<h>` derivation.
- **Labels:** `dev.buildagent.role=prior`, `.target-name`, `.update-id`.
- It is the original container, stopped and renamed — never a re-creation (I26).
- It is **residue** when no lock exists for the name it was renamed from, or a lock exists and is
  past its deadline. That test is sound only because the lock is keyed on the name and so outlives
  the rename.

---

## Public surface

### Image invocation

Declared by [`Dockerfile`](../Dockerfile). Protected, and each item below is a manifest item.

- Workdir `/workspace`; the consumer workspace is mounted there.
- `build <type>` is the build entry. Accepted types, exactly: `docker`, `node`, `node-in-docker`,
  `node-template`, `forge`. The set is **closed** — the brief's non-goals rule out adding build
  types.
- The container's exit status is the build's result, and both launchers return it unchanged.
- Environment inputs the image reads are manifest items. A newly read variable is an added item
  (compatible); a variable that stops being read is a removal and needs prior deprecation.

**Must never:** fall back to another image version, another tag, or a cached layer when the
requested version cannot be pulled. A launcher reports and exits non-zero instead.

### Config module — scaffold

```csharp
namespace Services;

public interface IConfigurationResolver
{
    // Locates, parses and validates the project file, then merges every tier.
    // Reports EVERY error found, not the first (I13).
    ResolutionResult Resolve(ResolutionRequest request);
}

public sealed record ResolutionRequest
{
    public required string RootDirectory { get; init; }
    public required IReadOnlyDictionary<string, string?> InvocationArguments { get; init; }
    public required IReadOnlyDictionary<string, string?> ModuleConfiguration { get; init; }
    public required IReadOnlyDictionary<string, string?> ProcessEnvironment { get; init; }
}

public sealed record ResolutionResult
{
    public required IReadOnlyList<ResolvedValue> Values { get; init; }
    public required IReadOnlyList<ConfigurationError> Errors { get; init; }
    public bool IsValid => Errors.Count == 0;
}
```

**Constraints the declaration cannot express:**

- `Resolve` **must not** acquire an overload or a flag that returns on first error. Reporting every
  error in one pass is I13, and a fail-fast overload would defeat it at every call site that chose
  it.
- `ResolutionRequest` **must not** acquire a parameter that changes precedence. Precedence is fixed
  (I16); a caller that can reorder tiers makes the order a suggestion.
- The result is in-memory only (I14). No overload writes it, and no `Save`-shaped member may be
  added.
- `node-template`'s script flow **calls this module**. It must not reimplement validation — one
  validation contract is the reason this module exists.

**Resolution order** (highest first; each tier shadows everything below):

1. Invocation arguments, including those a launcher passes through on a caller's behalf.
2. Module configuration (`Set-BuildAgentConfig`).
3. Process environment, including values `set-environment.ps1` sets.
4. Map-derived environment — which never overwrites an already-set variable (I18).
5. Project configuration file.
6. Declared defaults.

### Surface model — scaffold

```csharp
namespace Services;

public interface ISurfaceModel
{
    SurfaceManifest Derive();
    SurfaceComparison Compare(SurfaceManifest baseline, SurfaceManifest candidate);
}

public sealed record SurfaceComparison
{
    public required IReadOnlyList<SurfaceDifference> Differences { get; init; }
    public required IReadOnlyList<SurfaceDifference> Incompatible { get; init; }
}
```

**The comparison is a whitelist, and this is load-bearing.** `Incompatible` is every difference
**except** the enumerated compatible set:

- adding an item;
- marking an existing item deprecated;
- adding a supported schema version.

Removal of an item the baseline did not already mark deprecated is incompatible **at any major**.
Everything else is incompatible unless the major increases.

`Compare` **must not** acquire a list of forbidden difference kinds. A blacklist leaves each field
later added to the manifest unchecked and reports nothing when it does; a whitelist's failure mode
is a loud false failure a maintainer answers by naming the difference compatible.

`Derive` **must not** read a manifest from the tree or accept one as input (I6). Its only inputs
are the declarations themselves.

### Updater — scaffold

```csharp
namespace Services;

public interface IContainerUpdater
{
    UpdateResult Update(UpdateRequest request);
}

public sealed record UpdateRequest
{
    public required string ContainerName { get; init; }
    public required string ImageReference { get; init; }
    public int HealthTimeoutSeconds { get; init; } = 120;
    public bool Restore { get; init; } = true;
}

public sealed record UpdateResult
{
    public required UpdateOutcome Outcome { get; init; }
    public required UpdateExitCode ExitCode { get; init; }
    public string? UpdateId { get; init; }
    public UpdateError? Error { get; init; }
}
```

- `Restore` **must not** change its default to `false`. Automatic restore as the default is a brief
  commitment; flipping it turns every unattended failed update into an outage.
- `HealthTimeoutSeconds` is configurable but has no unbounded or zero setting: a non-positive value
  is a refusal, not "wait forever".
- `Update` **must never** remove a lock it did not create (I21), and **must never** re-create a
  container from inspect output on the restore path (I26).

**Ordered obligations:** the pull precedes the lock (I22); the start entry is flushed before the
first change (I24); the lock is released only after the outcome entry (I27).

**Configuration shapes that refuse before anything changes.** The design delegates this list here.
Two distinct refusals, which fail for different reasons and are never merged into one check:

*Cannot be restored exactly* — the target is auto-removed on stop (`HostConfig.AutoRemove`), or is
managed by an orchestrator (a `com.docker.swarm.*` or `io.kubernetes.*` label, or
`com.docker.compose.oneoff=True`).

*Cannot be created faithfully from inspect output* — the target has any anonymous volume (a mount
of type `volume` whose name the daemon generated), any legacy container link (`HostConfig.Links`
non-empty), more than one network attached at creation time, a network alias or static IP that
inspect output cannot round-trip, or any runtime option the client's API version does not surface
on create.

This list is a **floor, not a ceiling**: a shape whose fidelity cannot be established refuses.
Adding a shape to it is a compatible manifest difference; removing one is not.

### Operator commands — scaffold

Two commands exist solely because nothing is reclaimed automatically (I29). A command has no
separate declaration to point at, so each states its surface in full.

**Clear a lock.**
- Reads: the lock container's labels.
- Writes: removes exactly that lock container.
- Outputs: the lock's owner machine, process, update id, acquisition time and age.
- **Must not** touch the target container, the prior container, the pin or the log, and must not
  run as part of an update — only an operator invokes it.

**Complete a restore from residue.**
- Reads: the prior container's labels and the update log.
- Writes: removes the replacement if present, renames the prior container back, starts it, appends
  an outcome entry.
- Outputs: the interrupted update's id and the resulting container state.
- **Must not** run automatically, and **must not** proceed while a live lock for that name exists —
  a live lock means an owner may still be running.

### Global tool and PowerShell module

Both are launchers. Each translates one host invocation into one image invocation and returns the
container's exit status unchanged.

- Both pin the image to **their own version** by default (2026-09-20 decision). The reference stays
  overridable.
- Neither shares code with Build types. The dependency is on the image's published invocation
  surface only.
- Credentials in a Docker host URL are stripped from every message.
- The module's surface is [`PSModule.requirements.md`](../PSModule.requirements.md). The global tool
  additionally hosts the update and operator commands above.

---

## Error semantics

No bare exceptions and no string errors anywhere in this contract's surface.

### `ConfigurationError` — Config

```csharp
public enum ConfigurationErrorKind
{
    MultipleFilesPresent, Malformed, SchemaVersionMissing, SchemaVersionUnsupported,
    UnknownKey, SecretKeyRejected, ValueTypeMismatch, MapEntryUnresolved,
}

public sealed record ConfigurationError
{
    public required ConfigurationErrorKind Kind { get; init; }
    public required string FilePath { get; init; }
    public string? Key { get; init; }
    public required string Rule { get; init; }   // the rule broken, as the user sees it
}
```

| Variant | Raised when | Retryable | Caller does |
|---|---|---|---|
| `MultipleFilesPresent` | more than one `buildagent.*` file at the root | No | abort; the message names every file found |
| `Malformed` | the file does not parse | No | abort |
| `SchemaVersionMissing` | `schemaVersion` absent | No | abort |
| `SchemaVersionUnsupported` | outside the release's supported set | No | abort; the message names the supported set |
| `UnknownKey` | a key with no matching build parameter | No | abort |
| `SecretKeyRejected` | a secret-marked parameter appears in the file | No | abort; **the value is never echoed** |
| `ValueTypeMismatch` | a value does not match the parameter's declared type | No | abort |
| `MapEntryUnresolved` | a map-file entry resolves to empty | No | abort; the generated env file is removed |

All are collected, never thrown one at a time (I13). None is retryable: each is a defect in
committed input, and a retry reproduces it exactly.

### `ReleaseError` — Release pipeline

| Variant | Raised when | Retryable | Caller does |
|---|---|---|---|
| `VersionExists` | a published release, a draft for another SHA, or any sink holds the version without a matching claim | No | fail; the message names **which record** showed it |
| `MajorRegressed` | the candidate major is below the highest published major | No | fail |
| `TagPointsElsewhere` | the version tag exists on a different commit | No | fail |
| `SurfaceGate` | any manifest difference outside the compatible set | No | fail before the claim; list each item, what changed, and the rule it broke |
| `NotesSectionMissing` | a required section heading is absent | No | fail before the claim |
| `ManifestItemUnvaluable` | derivation cannot produce a comparable value for an item | No | fail; the item is reported rather than silently dropped (I7) |
| `SinkPublishFailed` | a sink fails after the claim exists | **Yes — same commit only** | stop, leave the claim a draft. A re-run for the same SHA resumes and skips sinks already holding the version under this claim. A re-run from a different SHA is refused |

Nothing is deleted on failure. Tags are immutable and published packages cannot be withdrawn
cleanly, so a partial publish is resumed, never rolled back.

### `UpdateError` — Updater

```csharp
public enum UpdateErrorKind
{
    ContainerNotFound, RefuseNotRestorable, RefuseUnfaithfulCreate, LockHeld,
    ResiduePresent, PullFailed, PinFailed, LogWriteFailed, DockerApiFailed, RestoreFailed,
}
```

| Variant | Raised when | Retryable | Caller does |
|---|---|---|---|
| `ContainerNotFound` | the name resolves to nothing | No | refuse; nothing changed |
| `RefuseNotRestorable` | auto-remove or orchestrator-managed | No | refuse; nothing changed |
| `RefuseUnfaithfulCreate` | a shape listed under *Updater* | No | refuse; nothing changed. This is the **only** guard on I23 |
| `LockHeld` | a lock exists | No | refuse. Report owner machine, process and age; past the deadline, say it is probably a dead update and name the clear command. **Never take it over** |
| `ResiduePresent` | a labelled prior container with no live lock, or an outcome-less start entry | No | refuse; report the residue and the interrupted update id |
| `PullFailed` | auth, rate limit, network, or an unknown reference | **Yes** | end with nothing changed and **no lock taken** — the pull is at step 3 |
| `PinFailed` | the pin tag operation fails | No | refuse ("the prior image cannot be kept"); nothing changed |
| `LogWriteFailed` — start entry | the state location is unwritable | No | refuse **and remove the pin just created** |
| `LogWriteFailed` — outcome entry | the state location is unwritable | No | the outcome still decides the exit status; stderr says the log write failed, and the stranded start entry becomes residue next run |
| `DockerApiFailed` | the API fails between the stop and the replacement starting | No | take the restore path **even when restore is disabled** — no updated container exists to keep |
| `RestoreFailed` | remove, rename-back or start fails during restore | No | stop. Write a `RestoreFailed` outcome, release the lock, leave the prior container as residue so the next update refuses rather than acting on broken state |

**Notification failure is never an error variant.** It warns, leaves the exit status unchanged, and
never puts the webhook URL in output.

### Exit codes — update command

```csharp
public enum UpdateExitCode
{
    Success = 0,
    Refused = 10,
    UnhealthyRestored = 20,
    UnhealthyNotRestored = 21,
    RestoreFailed = 22,
}
```

Each is a distinct observable outcome, because an operator script must be able to tell "I did
nothing" from "I changed things and put them back" from "I changed things and could not put them
back". `1` is reserved for an unexpected internal fault and is never a designed outcome. A no-op
update — the new image id equals the prior — is `Success` with outcome `NoOp`.

---

## Unresolved

1. **The global tool's .NET package id and its invoked command name.** The design fixes the tool's
   *subcommands* and their semantics, but neither the design nor the brief names the executable,
   and it becomes a protected surface the moment it first publishes — so guessing it here would
   mint a compatibility commitment out of nothing. Blocked on `10-design.md` Open question 1
   (package identity ownership and key custody), an operational prerequisite to the first 2.0.0
   publish. **No slice that declares the tool's public entry point can be written until this is
   decided**; slices for the updater's internals are not blocked by it.
