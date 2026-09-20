# Slices — Docker-BuildAgent 2.0.0

Input: [`10-design.md`](10-design.md) and [`20-contract.md`](20-contract.md). Each slice below is
vertical: it reaches from an entry point a person invokes to whatever it persists, and it leaves the
system runnable.

**Order.** The design makes two bets that everything else rests on, so both are exercised first.
S1 bets that the public surface can be derived mechanically from declarations and compared between
releases — if it cannot, the compatibility promise has no enforcement and the release gate is
decoration. S2 bets that a created-but-never-started container named from the *target's name* gives
a mutual exclusion that holds across every client of the daemon, including through the rename in
the middle of the critical section. Neither bet is provable on paper.

**Issue coverage.** #1 → S2, S4, S5. #11 → S6. #12 → S6, S7, S8. #14 → *Blocked*, below.

## How this document is kept

Slices move from `## Outstanding` to `## Landed` as they ship. A re-run appends to `## Outstanding`
only; `## Landed` is never rewritten. Criterion ids are permanent: they are what the tracker matches
on, so a removed criterion leaves a gap and the next one continues past it. Ids are never reused and
never renumbered.

## Landed

*None yet.*

## Outstanding

## S1 — The public surface, derived and compared
Delivers: Maintainers can no longer publish a release that quietly drops or renames something public
users depend on. Every release produces a machine-readable list of what the product exposes, and a
release stops with an explicit explanation when that list changes in a way the compatibility promise
does not allow.
Touches: the surface model and its manifest and difference types, the build parameter classes it
derives from, the release workflow's gate step.
Depends on: none.
Acceptance:
  - S1.1 Deriving the manifest twice from an unchanged tree produces byte-identical output.
  - S1.2 The manifest holds one item per public build parameter declared in the `*Params` classes,
    one per accepted `build` type, one per Docker-template discovery location in discovery order,
    and one per supported project-file schema version.
  - S1.3 Derivation never reads a manifest from the tree: with every manifest file deleted from the
    working tree, derivation still succeeds and produces the same output as S1.1.
  - S1.4 An item whose value cannot be produced fails derivation with `ManifestItemUnvaluable`
    naming the item, and no manifest is written.
  - S1.5 A candidate that only adds an item, only marks an existing item deprecated, or only adds a
    supported schema version reports zero incompatible differences.
  - S1.6 Renaming a build parameter reports two differences — one `Removed` and one `Added` — and
    the `Removed` one is incompatible.
  - S1.7 Removing an item the baseline did not already mark deprecated is incompatible even when the
    candidate major is higher than the baseline major.
  - S1.8 Changing an existing item's value is incompatible at the same major, and the failure names
    the item, its baseline value and its candidate value.
  - S1.9 Reordering the Docker-template discovery locations is reported as `Reordered` and is
    incompatible at the same major.
  - S1.10 A difference the comparison has no compatibility rule for is reported incompatible. Adding
    a new field to the manifest without touching the comparison makes releases fail loudly rather
    than pass silently.
  - S1.11 The manifest is attached to the release as an asset, and the gate reads the previous
    release's attached manifest as its baseline. With no previous manifest available the gate fails
    rather than passing.
  - S1.12 On any incompatible difference the gate exits non-zero before a claim exists and before
    any sink is written, listing each incompatible item, what changed, and the rule it broke.
Out of scope: the release claim, publishing to any sink, and the release-notes checks — S3 owns all
three. Changing a declaration to make derivation succeed is in scope only where derivation fails;
renaming or removing a parameter is not.

## S2 — Update a running container, or refuse without touching it
Delivers: Someone can move a long-running container to a new image with one command. When the update
could not be undone — another update already running, leftovers from an interrupted one, or a
container whose setup cannot be reproduced exactly — the command refuses and the container is left
exactly as it was.
Touches: the container updater and its request, result and error types, the lock container and its
labels, the prior image pin, the prior container, the update log.
Depends on: none.
Acceptance:
  - S2.1 Two updates of the same container name started at once: exactly one enters the critical
    section, the other exits `10` with `LockHeld`, and the target's id, image and state are
    unchanged.
  - S2.2 While the first update has the target renamed out of the way, a second updater invoked with
    the original container name still finds the lock and refuses — the lock survives the id behind
    the name changing.
  - S2.3 Acquiring the lock performs no registry pull: with the image already present locally and
    the daemon unable to reach the registry, the lock is still acquired.
  - S2.4 An unknown image reference fails with `PullFailed`, exits non-zero, leaves no lock container
    behind, and changes nothing.
  - S2.5 A lock whose deadline has passed still refuses with `LockHeld` and exit `10`. The message
    names the owner machine, the owner process, the lock's age and the command that clears it, and
    the lock is not removed.
  - S2.6 A target with auto-remove set refuses with `RefuseNotRestorable` and exit `10`.
  - S2.7 A target carrying a Swarm, Kubernetes or Compose one-off label refuses with
    `RefuseNotRestorable` and exit `10`.
  - S2.8 A target with an anonymous volume, a legacy container link, more than one network attached
    at creation, or a network alias or static IP that inspect output cannot round-trip refuses with
    `RefuseUnfaithfulCreate` and exit `10`, naming the specific shape found.
  - S2.9 A labelled prior container with no live lock for its target name refuses with
    `ResiduePresent` and exit `10`, naming the interrupted update's id.
  - S2.10 Every refusal above leaves the target's container id, image id, state and start count
    unchanged and writes no update-log entry.
  - S2.11 A failure to pin the prior image refuses with `PinFailed` and exit `10`, changing nothing.
  - S2.12 A failure to write the start entry refuses with exit `10` and removes the prior image pin
    created moments earlier.
  - S2.13 On success the replacement container runs the new image, and its environment, command,
    mounts, ports, restart policy, networks and labels — excluding the product's own — equal the
    target's before the update.
  - S2.14 After success exactly one prior image pin exists for that target, pointing at the
    pre-update image id.
  - S2.15 The start entry is flushed before the first change: killing the process immediately after
    the stop leaves a start entry with no matching outcome entry in the log.
  - S2.16 The lock is released only after the outcome entry is written: killing the process between
    them leaves the lock in place, and the next update refuses rather than proceeding.
  - S2.17 No update-log entry contains container environment, command or mount values.
  - S2.18 When the pulled image id equals the target's current image id the command exits `0` with
    outcome `NoOp` and nothing changes.
  - S2.19 These criteria are verified automatically on Linux. On Windows only the host-independent
    parts — argument translation, Docker host resolution, path and mount handling — are covered;
    live-daemon update behaviour on a Windows host running Docker Desktop with Linux containers is a
    stated, unverified gap, recorded in the slice's own output rather than left implied.
Out of scope: health verification, automatic restore and the restore exit codes — S4 owns them, and
until it lands an API failure mid-swap leaves residue that the next update refuses on. The operator
recovery commands (S5). Notifications.

## S3 — A version publishes exactly once
Delivers: A maintainer can publish a version once and only once. A second attempt at a version that
already exists stops with a clear reason instead of overwriting anything, an interrupted publish
resumes without duplicating what already went out, and a push to the main branch no longer produces
a versioned artifact at all.
Touches: the release version, claim and error types, the release workflow, the main-push workflow.
Depends on: S1.
Acceptance:
  - S3.1 Publishing a version a previous release published fails with `VersionExists` before any
    sink is written, and names which record showed it.
  - S3.2 A draft claim bound to a different commit fails with `VersionExists`, naming that commit.
  - S3.3 A candidate major below the highest published major fails with `MajorRegressed`.
  - S3.4 A version tag that exists on a different commit fails with `TagPointsElsewhere`.
  - S3.5 Release notes missing either the breaking-changes or the deprecations heading fail with
    `NotesSectionMissing` before the claim is created. An empty section under a present heading
    passes.
  - S3.6 The claim is created before any sink is written: with every publish step forced to fail,
    the draft claim exists and no sink holds the version.
  - S3.7 The version is computed once and stamped into every artifact the release produces, which
    carry byte-identical version strings.
  - S3.8 `latest` moves only after every versioned sink holds the release; failing the last
    versioned sink leaves `latest` where it was.
  - S3.9 Re-running a failed publish from the same commit skips sinks already holding the version
    under this claim and completes the rest. The claim becomes published only when every sink holds
    it.
  - S3.10 Re-running a failed publish from a different commit is refused and writes nothing.
  - S3.11 Two publishing workflows started at once do not both create a claim for the same version.
  - S3.12 A push to `main` moves `latest` and writes no version to any versioned sink — no tag, no
    package, no module version.
  - S3.13 No failure path deletes a tag, a package or a release.
Out of scope: the surface gate itself (S1); the .NET tool feed and the tool's package identity,
which are blocked; documenting the promise (S11).

## S4 — When the new image does not come up, the old one comes back
Delivers: When an updated container does not report healthy, the previous version returns on its
own, and whoever ran the update learns from the exit status exactly which of three things happened:
nothing changed, it changed and was put back, or it changed and could not be put back.
Touches: the updater's health wait and restore path, the outcome and exit-code types, the prior
container and its labels, the update log's outcome entry.
Depends on: S2.
Acceptance:
  - S4.1 A replacement reporting healthy within the timeout exits `0` with outcome `Succeeded`, and
    the prior container is removed.
  - S4.2 A replacement that does not report healthy within the timeout is removed, the prior
    container is renamed back and started, the command exits `20` with outcome `UnhealthyRestored`,
    and the running container's image id equals the pre-update image id.
  - S4.3 A target declaring no health check is treated as not passing: the prior image is restored
    and the command exits `20`.
  - S4.4 With restore disabled, an unhealthy replacement is left in place, the command exits `21`
    with outcome `UnhealthyNotRestored`, and the prior container and pin remain for manual recovery.
  - S4.5 Restore is enabled when the caller does not say otherwise.
  - S4.6 A health timeout of zero or less refuses with exit `10` and changes nothing; no value means
    "wait forever".
  - S4.7 Restore renames the retained prior container back and starts it. Removing that prior
    container before restore produces `RestoreFailed` rather than a container re-created from
    inspect output.
  - S4.8 A failure during restore exits `22` with outcome `RestoreFailed`, writes the outcome entry,
    releases the lock, and leaves the prior container in place so the next update refuses.
  - S4.9 A Docker API failure between the stop and the replacement starting takes the restore path
    even when restore is disabled.
  - S4.10 A failure to write the outcome entry does not change the exit status: the failure is
    reported on stderr, and the stranded start entry is detected as residue by the next update.
  - S4.11 A notification failure warns, leaves the exit status unchanged, and puts no webhook URL in
    any output stream.
  - S4.12 Exit codes `0`, `10`, `20`, `21` and `22` are each produced by at least one test, and `1`
    by none.
Out of scope: the operator recovery commands (S5); restoring volumes, data, host configuration or
consumer state; any health monitoring after the command returns.

## S5 — Recovering by hand from an update that was killed
Delivers: When an update is interrupted — a dropped connection, a rebooted host — an operator has
two named commands that say exactly what was left behind and put the container back, instead of
reconstructing the situation from Docker state by hand. Nothing is reclaimed automatically.
Touches: the two operator commands, the lock container's labels, the prior container's labels, the
update log.
Depends on: S4.
Acceptance:
  - S5.1 The clear-lock command removes exactly the named container's lock and reports its owner
    machine, owner process, update id, acquisition time and age.
  - S5.2 The clear-lock command leaves the target container, the prior container, the prior image
    pin and the update log untouched.
  - S5.3 The complete-restore command removes the replacement if present, renames the prior
    container back, starts it, and appends an outcome entry naming the interrupted update's id.
  - S5.4 The complete-restore command refuses while a live lock exists for that container name.
  - S5.5 Neither command is invoked by any update path; both run only when an operator invokes them.
  - S5.6 After complete-restore the running container's image id equals the pre-update image id, and
    the next update of that container no longer refuses on residue.
  - S5.7 An interrupted update is never reclaimed by the next update — it refuses and names the
    operator command to run.
Out of scope: any automatic reclaim; recovering volumes or data; a background watcher.

## S6 — One configuration file per project, validated completely
Delivers: A project can keep its build settings in a single YAML or JSON file at its root instead of
spreading them across command lines and environment variables, and a mistake in that file is
reported in full, before the build starts, rather than partway through.
Touches: the configuration resolver and its error types, project-file discovery and parsing, the
`node-template` script flow.
Depends on: none.
Acceptance:
  - S6.1 A `buildagent.yaml`, `buildagent.yml` or `buildagent.json` at the project root is
    discovered; a file of another name, or in another directory, is not.
  - S6.2 Two matching files at the root fail with `MultipleFilesPresent` naming every file found —
    never a precedence decision between them.
  - S6.3 A missing `schemaVersion` fails with `SchemaVersionMissing`.
  - S6.4 A `schemaVersion` outside the release's supported set fails with `SchemaVersionUnsupported`
    and the message names the supported set.
  - S6.5 A key with no matching build parameter fails with `UnknownKey` naming the key.
  - S6.6 A secret-marked parameter present in the file fails with `SecretKeyRejected`, and its value
    appears in no output stream.
  - S6.7 A value whose type does not match the parameter's declared type fails with
    `ValueTypeMismatch`.
  - S6.8 A file that does not parse fails with `Malformed`.
  - S6.9 A file containing four distinct errors reports all four in one pass, before any build step
    runs.
  - S6.10 Every error above exits non-zero and runs no build step.
  - S6.11 Resolution creates and modifies no file: the resolved configuration is never written to
    disk.
  - S6.12 The project file's bytes are identical before and after a build, including a build that
    fails validation — the product never migrates or rewrites a consumer's file.
  - S6.13 The `node-template` flow calls this module rather than validating on its own: an unknown
    key fails identically whether the build starts as `build node-template` or as any other type.
  - S6.14 Automated tests cover, and report the count of, an unknown key, both formats present, a
    missing schema version, an unsupported schema version, and a malformed file.
Out of scope: precedence between configuration sources (S7); the sample files (S8); removing or
deprecating any configuration surface that exists today.

## S7 — One predictable precedence across every source
Delivers: Someone who sets the same setting in two places can tell which one wins without
experimenting, and can see for every effective value which source it came from.
Touches: the resolver's merge, the resolved-value and tier types, mapping-file expansion, generated
environment files.
Depends on: S6.
Acceptance:
  - S7.1 For a key set in all six sources, the effective value is the invocation argument, and its
    recorded source says so.
  - S7.2 Removing each source in turn from that case yields, in order, the module configuration, the
    process environment, the map-derived environment, the project file, and then the declared
    default — with the recorded source matching at every step.
  - S7.3 Every resolved value records the source it came from; none is recorded as unknown.
  - S7.4 A map-derived value does not overwrite a variable already set in the process environment.
  - S7.5 A map entry that resolves to empty fails with `MapEntryUnresolved`, and the generated
    environment file is removed.
  - S7.6 Generated environment files are removed after a successful build, after a failed build, and
    when the process is terminated by a signal.
  - S7.7 A secret-marked parameter is redacted in every output stream whichever source it came from,
    and redaction covers every secret-marked parameter rather than values matching a token-shaped
    pattern.
  - S7.8 The resolver exposes no overload or flag that returns on the first error, and no member
    that writes the resolved configuration.
  - S7.9 The resolution request exposes no parameter that reorders the sources.
  - S7.10 The project-file key set equals the build parameter vocabulary: adding a parameter to a
    `*Params` class makes its key accepted, and no accepted key exists without a matching parameter.
Out of scope: adding or removing any configuration source; changing what the existing mapping files
mean; persisting the resolved configuration in any form.

## S8 — A working example for every build type, in both formats
Delivers: Someone adopting the project configuration file can start from a working example for their
build type in whichever format they prefer, instead of assembling one from reference documentation.
Touches: the sample files and the test that validates them.
Depends on: S6, S7.
Acceptance:
  - S8.1 Ten samples exist — one YAML and one JSON for each of `docker`, `node`, `node-in-docker`,
    `node-template` and `forge`.
  - S8.2 Each sample passes validation unedited, and the test reports the count validated.
  - S8.3 Each sample declares a `schemaVersion` in the supported set.
  - S8.4 No sample contains a secret-marked parameter or a placeholder credential.
  - S8.5 The YAML and the JSON sample for a build type express the same settings.
  - S8.6 A sample that stops validating fails the build, so a parameter change cannot silently
    invalidate the samples.
Out of scope: documenting the samples on the published site (S11); adding parameters only to make a
sample look richer.

## S9 — The PowerShell module runs its own image version
Delivers: Someone who pins a module version now gets the build image that matches it, so a pinned
setup stops changing underneath them whenever a new image is published. The old behaviour is called
out as a breaking change with a migration step.
Touches: the module's default configuration, `PSModule.requirements.md`, the module's tests, the
release notes and the migration guide.
Depends on: S3.
Acceptance:
  - S9.1 With no image configured, the module's effective image reference is its own version, not
    `latest`.
  - S9.2 The image reference stays overridable through `Set-BuildAgentConfig`, and an override is
    used verbatim.
  - S9.3 `PSModule.requirements.md` states the version-pinned default, and no document still states
    `latest` as the module's default.
  - S9.4 The change appears in the 2.0.0 release notes' breaking-changes section and in the
    migration guide.
  - S9.5 The module's tests pass on Windows PowerShell 5.1 and on PowerShell 7, and the run reports
    both.
  - S9.6 A requested image version that cannot be pulled fails with a non-zero exit and no fallback
    to another version, another tag or a cached layer.
  - S9.7 Credentials in a Docker host URL are stripped from every message the module emits.
  - S9.8 The module's exported members remain exactly `Set-BuildAgentConfig`, `Invoke-Build` and
    `BuildAgentConfig`.
Out of scope: publishing the module to the PowerShell Gallery, which waits on publishing-key
custody; any other module default; the global tool's launcher.

## S10 — Documentation cannot name what the product does not have
Delivers: A reader of the documentation site, the README or the PowerShell help can trust that every
command, option and path named there actually exists, because a change introducing one that does not
is stopped before it merges.
Touches: the documentation check, the surface manifest it reads, the pull-request workflow.
Depends on: S1.
Acceptance:
  - S10.1 The check runs on every pull request and fails it on a violation.
  - S10.2 A documented command, parameter, build type or discovery location absent from the manifest
    fails the check, naming the document, the line and the missing name.
  - S10.3 A documented repository path or linked tree file that does not exist fails the check.
  - S10.4 The check covers the documentation site sources, the README and the PowerShell help.
  - S10.5 A surface that is deprecated but still present does not fail the check.
  - S10.6 The check's output states which classes of claim it does not cover, and it makes no claim
    about behavioural statements.
  - S10.7 Run against the tree as it stands when the slice starts, the check reports its findings
    rather than being tuned to pass; each finding is either fixed or recorded.
  - S10.8 The check does not read `docs-template/`, which is pinned and not owned here.
Out of scope: rewriting documentation prose (S11); verifying behavioural claims; changing
`docs-template/`.

## S11 — One page that states the promise, and a way to move to 2.0.0
Delivers: A public user can read in one place what is protected, how versions work, which versions
get fixes, how deprecation works, which hosts and CI are supported, and the Docker socket warning.
Someone on 1.x can follow a guide that covers every breaking change and the move from a floating tag
to a pinned version.
Touches: the published documentation site, the compatibility page, the deprecation policy, the
migration guide, the release-notes template.
Depends on: S3, S9, S10.
Acceptance:
  - S11.1 One published page states the protected surfaces, the versioning rules, which versions
    receive fixes, the deprecation policy, supported hosts and CI, and the Docker socket trust
    warning, and names the contract as canonical.
  - S11.2 The page states that `latest` is movable and shows how to select a versioned image.
  - S11.3 The page states that image contents are protected only where the contract names them, and
    that bundled tool versions are not protected unless named.
  - S11.4 The deprecation policy states that a surface is deprecated in a minor release, warns when
    used, and is removed no earlier than the next major.
  - S11.5 The migration guide covers every breaking change in 2.0.0, including the module's image
    default, and covers moving from `latest` to a pinned version.
  - S11.6 The release-notes template carries both required headings, and a release without them
    cannot publish.
  - S11.7 The page passes the S10 check.
Out of scope: classifying the rest of the documentation (S12); writing per-build-type reference
documentation.

## S12 — Every document about a public surface has one owner
Delivers: Anyone reading about a public surface lands on the one document that defines it, because
every other document that covered the same ground now either points at it or is gone.
Touches: the README, the product instructions, the published documentation sources, the PowerShell
documents, the historical design reports.
Depends on: S10, S11.
Acceptance:
  - S12.1 Every document overlapping a public surface is classified as a canonical contract, a
    reference naming its canonical source, or removed, and the classification is recorded in one
    list.
  - S12.2 The `build` command, project configuration, Docker-template discovery, the global tool and
    the PowerShell module each have exactly one canonical contract, and `PSModule.requirements.md`
    is the module's.
  - S12.3 Every reference document names the canonical contract it explains.
  - S12.4 No two documents state the same rule in their own words without one naming the other as
    canonical.
  - S12.5 Removed documents are gone from the published site and remain in git history only.
  - S12.6 No document still disagrees with the tree on a name the S10 check covers.
Out of scope: rewriting reference prose beyond adding the canonical pointer; changing
`docs-template/`.

## Blocked

**The global tool's published entry point — issue #14.** No slice can be written for it.
[`20-contract.md`](20-contract.md) § *Unresolved* leaves the .NET package id and the invoked command
name undecided, and a slice naming either would introduce a signature the contract does not carry,
which the slice rules forbid. This blocks installing the tool from the public .NET tool feed,
exposing the five build types through it, and giving the update and operator commands a
host-invokable name. It does not block S2, S4 or S5, which are written against the updater itself.

Answering [`10-design.md`](10-design.md) Open question 1 — package identity ownership and key
custody — unblocks it. The same question's key-custody half blocks publishing the PowerShell module
to the PowerShell Gallery, although the module's name is already settled.

---

Run `/track` next to open issues from these slices. This document opens none.
