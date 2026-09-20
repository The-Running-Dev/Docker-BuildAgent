# Slices — Docker-BuildAgent 2.0.0

Input: [`10-design.md`](10-design.md) and [`20-contract.md`](20-contract.md). Every name, error
code, exit status and invariant id below comes from the contract; this document introduces none.
Each slice is vertical: it reaches from an entry point a person invokes to whatever it persists, and
it leaves the system runnable.

**Order.** The design makes two bets that everything else rests on, so both are exercised first.
S1 bets that the public surface can be derived mechanically from declarations and compared against
an immutable asset of an older release — if it cannot, the compatibility promise has no enforcement
and the gate is decoration. S2 bets that a created-but-never-started container, named from a hash of
the *target's name* and attributed by label, gives a mutual exclusion that holds across every client
of the daemon, including through the rename in the middle of the critical section. Neither is
provable on paper.

**Issue coverage.** #1 → S2, S4, S5. #11 → S6. #12 → S6, S7, S8. #14 → *Blocked*, below.

## How this document is kept

Slices move from `## Outstanding` to `## Landed` as they ship. A re-run appends to `## Outstanding`
only; `## Landed` is never rewritten. Criterion ids are permanent: they are what the tracker matches
on, so a withdrawn criterion leaves a gap and the next id continues past it. Ids are never reused
and never renumbered. Gaps in the numbering below are withdrawn criteria, listed with each slice.

## Outstanding

## S1 — The public surface, derived and compared
Delivers: Maintainers can no longer publish a release that quietly drops or renames something public
users depend on. Every release produces a machine-readable list of what the product exposes, and a
release stops with an explicit explanation when that list changes in a way the compatibility promise
does not allow.
Touches: the surface model and its manifest, item and difference types; the manifest asset schema;
the build parameter classes it derives from; the release workflow's gate step.
Depends on: none.
Acceptance:
  - S1.1 Deriving the manifest twice from an unchanged tree produces byte-identical output.
  - S1.2 The manifest holds one item per public build parameter, one per accepted `build` type, one
    per Docker template discovery location in discovery order, one per exported PowerShell module
    member, and one per supported project-file schema version.
  - S1.3 Derivation never reads a manifest from the working tree: with every manifest file deleted
    from the tree, derivation still succeeds and produces the same output as S1.1 (I8).
  - S1.4 An item whose value cannot be produced as a stable ordinal string fails with
    `DerivationFailed` naming the item, and no manifest is written.
  - S1.5 A candidate that only adds an item, only marks an existing item deprecated, or only adds a
    supported schema version reports zero blocking differences.
  - S1.6 Renaming a build parameter reports two differences — one removal and one addition — and the
    removal is a `BlockingDifference`.
  - S1.7 An item present in a published manifest and absent from the candidate is a removal whatever
    the reason for its absence, including a value that became underivable (I11).
  - S1.8 Changing an existing item's value blocks the release when the major does not increase, and
    the failure names the item, its baseline value and its candidate value.
  - S1.9 Reordering the Docker template discovery locations is a `BlockingDifference` when the major
    does not increase.
  - S1.10 A difference kind the comparison has no compatibility rule for is blocking, not passing
    (I9). Adding a field to the manifest without touching the comparison makes releases fail
    loudly rather than pass silently.
  - S1.11 The candidate manifest is attached to the release as an asset, and the gate reads the
    highest published release's manifest asset as its baseline. No baseline asset fails with
    `BaselineMissing` rather than passing; an undownloadable or unparseable one fails with
    `BaselineUnreadable`.
  - S1.12 On any blocking difference the gate exits non-zero before a claim exists and before any
    sink is written, listing every blocking difference with what changed and the rule it broke.
  - S1.13 Two items sharing a `(Kind, Name)` fail with `DuplicateItem`.
  - S1.14 A manifest whose `manifestSchemaVersion` the reader does not know fails with
    `ManifestSchemaUnsupported`; it is never treated as an empty or partial baseline (I12).
Out of scope: the release claim, publishing to any sink, and the release-notes check — S3 owns all
three. Changing a declaration to make derivation succeed is in scope only where derivation fails;
renaming or removing a parameter is not.

## S2 — Update a running container, or refuse without touching it
Delivers: Someone can move a long-running container to a new image with one command. When the update
could not be undone — another update already running, leftovers from an interrupted one, or a
container whose setup cannot be reproduced exactly — the command refuses and the container is left
exactly as it was.
Touches: the Updater and its error type; the lock container, the prior container and the prior image
pin, with their names and `com.buildagent.container` labels; the update log; the tool's `update`
command surface.
Depends on: none.
Acceptance:
  - S2.1 Two updates of the same container started at once: exactly one enters the critical section,
    the other exits `21` with `LockHeld`, and the target's id, image and state are unchanged.
  - S2.2 While the first update has the target renamed out of the way, a second updater invoked with
    the original container name still finds the lock and refuses — the lock survives the id behind
    the name changing.
  - S2.3 Acquiring the lock performs no registry pull: with the image already present locally and
    the daemon unable to reach the registry, the lock is still acquired (I33).
  - S2.4 An image that cannot be obtained fails with `ImageUnavailable`, exits `30`, leaves no lock
    container behind, and changes nothing.
  - S2.5 A lock whose deadline has passed still refuses with `LockHeld` and exit `21`. The message
    names the owning host, the process and the lock's age, says the deadline has passed, and says
    the deadline does not authorise a takeover. The lock is not removed (I31, I32).
  - S2.6 A target running with `--rm` refuses with `TargetAutoRemove` and exit `20`.
  - S2.7 A target carrying orchestrator ownership labels refuses with `TargetOrchestratorManaged`
    and exit `20`.
  - S2.8 A target with an anonymous volume, or with a legacy container link, refuses with
    `TargetShapeUnsupported` and exit `20`, naming the element found. These are the two shapes the
    design states; the rest of the list is `## Unresolved` U-10 and this slice does not extend it.
  - S2.9 A prior container, or an open log record with no outcome, refuses with `ResiduePresent` and
    exit `22`, naming the object and the command that clears it.
  - S2.10 Every refusal above leaves the target's container id, image id, state and start count
    unchanged and writes no update-log entry.
  - S2.11 A prior image that cannot be pinned refuses with `PinFailed` and exit `23` before any
    change.
  - S2.12 A start entry that cannot be flushed refuses with `LogUnwritable` and exit `24` before any
    change, and removes the prior image pin created moments earlier (I36).
  - S2.13 On success the replacement container runs the new image, and its environment, command,
    mounts, ports, restart policy, networks and labels — excluding the product's own — equal the
    target's before the update.
  - S2.14 After success exactly one prior image pin exists for that target, tagged
    `buildagent-prior:<h>`, carrying the target's full name in its `com.buildagent.container` label,
    and pointing at the pre-update image id.
  - S2.15 Residue detection reads the `com.buildagent.container` label, never the object's name, to
    decide which target an object belongs to.
  - S2.16 The start entry is flushed before the first change: killing the process immediately after
    the stop leaves a start record with no outcome record in the log.
  - S2.17 The lock is released only after the outcome record is written: killing the process between
    them leaves the lock in place, and the next update refuses rather than proceeding.
  - S2.18 No update-log record contains container environment, command or mount values.
  - S2.19 A log line that cannot be parsed at all refuses every update on that host until an
    operator clears it; a record carrying an unknown `recordSchemaVersion` is still read for its
    update id, container name and the presence of an outcome, and does not trigger that refusal.
  - S2.20 When the resolved image id equals the target's current image id the command exits `0` with
    outcome `AlreadyCurrent` and nothing changes.
  - S2.21 A target name that resolves to no container fails with `TargetNotFound` and exit `20`.
  - S2.22 The round-trip probe U-10 requires is run against the daemon and its per-shape results are
    recorded for adjudication. The refusal list is not widened by this slice on the strength of that
    probe alone.
  - S2.23 These criteria are verified automatically on Linux. On Windows only the host-independent
    parts — argument translation, Docker host resolution, path and mount handling — are covered.
    Live-daemon update behaviour on a Windows host running Docker Desktop with Linux containers is a
    stated, unverified gap, recorded in the slice's own output rather than left implied.
Out of scope: health verification, restore and the restore exit statuses — S4 owns them, and until
it lands a failure mid-swap leaves residue that the next update refuses on. Operator recovery (S5).
Notifications, whose module boundary is `## Unresolved` U-8.

## S3 — A version publishes exactly once
Delivers: A maintainer can publish a version once and only once. A second attempt at a version that
already exists stops with a clear reason instead of overwriting anything, and a push to the main
branch no longer produces a versioned artifact at all.
Touches: the release version, claim and error types; the release workflow; the main-push workflow;
the CI concurrency group.
Depends on: S1.
Acceptance:
  - S3.1 A version already held by a claim, a versioned image tag or a git tag fails with
    `VersionAlreadyExists` before any write, naming which of the three held it (I5).
  - S3.2 A draft claim bound to a different commit fails with `VersionAlreadyExists`, naming that
    commit.
  - S3.3 A candidate major below the highest published major fails with `MajorBelowCurrent` before
    any write.
  - S3.4 A git tag for the version on another commit fails with `TagPointsElsewhere`.
  - S3.5 Release notes lacking either the breaking-changes or the deprecations heading fail with
    `NotesSectionMissing` before any write. An empty section under a present heading passes
    (I7).
  - S3.6 The claim is the first write of a release: with every publish step forced to fail, the
    draft claim exists and no sink holds the version (I3).
  - S3.7 The version is computed once and stamped into every artifact the release produces, which
    carry byte-identical version strings (I1).
  - S3.10 A publish attempted outside the CI publishing context fails with `NotPublishedByCi` and
    writes nothing (I14).
  - S3.11 A single CI concurrency group spans every publishing workflow and queues rather than
    cancels, so at most one run publishes at a time (I13).
  - S3.12 A push to `main` moves `latest` and writes no version to any versioned sink — no tag, no
    package, no module version (I15).
  - S3.13 No failure path deletes a tag, a package or a release.
  - S3.14 A sink that rejects a write fails with `SinkPublishFailed` naming the sink, and leaves the
    claim open rather than deleting it.
Withdrawn: S3.8 (the ordering of the `latest` move relative to the last versioned sink) and S3.9
(what a re-run may conclude from an existing sink artifact) are withdrawn. Both depend on
unadjudicated red-team findings — `## Unresolved` U-9 and U-6 — and writing either as a criterion
would presume an answer the contract does not carry.
Out of scope: the surface gate itself (S1); the .NET tool feed and the tool's package identity,
which are blocked; documenting the promise (S11).

## S4 — When the new image does not come up, the old one comes back
Delivers: When an updated container does not report healthy, the previous version returns on its
own, and whoever ran the update learns from the exit status exactly which of three things happened:
nothing changed, it changed and was put back, or it changed and could not be put back.
Touches: the Updater's health wait and restore path; the outcome and exit-status surface; the prior
container and its labels; the update log's outcome record.
Depends on: S2.
Acceptance:
  - S4.1 A replacement reporting healthy within the timeout exits `0` with outcome `Succeeded`, and
    the prior container is removed.
  - S4.2 A replacement that does not pass its check within the timeout fails with `HealthTimedOut`;
    the replacement is removed, the prior container is returned to its name and started, and the
    command exits `10` with outcome `RestoredAfterUnhealthy`. The running container's image id
    equals the pre-update image id.
  - S4.3 A target declaring no health check fails with `HealthCheckAbsent` and is treated as
    unhealthy, restoring per the restore setting.
  - S4.4 A replacement that exits before becoming healthy fails with `ReplacementExited` and takes
    the same restore path.
  - S4.5 With `--no-restore`, an unhealthy replacement is left in place and the command exits `11`
    with outcome `UnhealthyNotRestored`; the prior container and its pin remain for manual recovery.
  - S4.6 Restore is the default; omitting `--no-restore` restores.
  - S4.7 `--health-timeout` defaults to 120 seconds, and a value of zero or less refuses rather than
    waiting indefinitely.
  - S4.8 Restore returns the retained prior container to its name and starts it. Removing that prior
    container beforehand produces `RestoreFailed` rather than a container re-created from inspect
    output.
  - S4.9 `RestoreFailed` exits `12` naming the prior container and its labels, writes the outcome
    record, releases the lock, and leaves the prior container in place so the next update refuses.
  - S4.10 A daemon rejection during creation fails with `ReplacementCreateFailed` and restores
    before exiting, since no updated container exists to keep.
  - S4.11 A failure to write the outcome record does not change the exit status: it is reported on
    stderr, and the stranded start record is detected as residue by the next update.
  - S4.12 Exit statuses `0`, `10`, `11` and `12` are each produced by at least one test, and `1` by
    none.
Withdrawn: S4.13 (notification behaviour on an update) is withdrawn — Notifications' module boundary
relative to the Updater is `## Unresolved` U-8, so which component owns the warning is undetermined.
Out of scope: operator recovery (S5); restoring volumes, data, host configuration or consumer state;
any health monitoring after the command returns.

## S5 — Clearing a lock left by an update that was killed
Delivers: When an update is interrupted — a dropped connection, a rebooted host — an operator has a
named command that reports what the lock says and removes it, instead of reconstructing the
situation from Docker state by hand. Nothing is reclaimed automatically.
Touches: the tool's `update --clear-lock` command; the lock container and its
`com.buildagent.container` label.
Depends on: S4.
Acceptance:
  - S5.1 `update --clear-lock <container>` removes exactly that target's lock and reports the lock's
    owning host, process, update id, acquisition time and age.
  - S5.2 It leaves the target container, the prior container, the prior image pin and the update log
    untouched, and takes no other action (I31).
  - S5.3 It is the only operator action that removes a lock it does not own, and no update path
    invokes it.
  - S5.4 A refusal naming a lock names this command as the action that clears it.
Withdrawn: S5.5, S5.6 and S5.7 (completing a restore from a prior container left behind by an
interrupted update) are withdrawn. The contract's tool surface carries `update --clear-lock` and no
command that recovers prior-container residue, so a slice for it would introduce a signature the
contract does not have. Recorded under *Blocked*.
Out of scope: any automatic reclaim; recovering volumes or data; a background watcher.

## S6 — One configuration file per project, validated completely
Delivers: A project can keep its build settings in a single YAML or JSON file at its root instead of
spreading them across command lines and environment variables, and a mistake in that file is
reported in full, before the build starts, rather than partway through.
Touches: Config's discovery, parsing and validation; its error type; the project configuration file
schema.
Depends on: none.
Acceptance:
  - S6.1 A `buildagent.yml`, `buildagent.yaml` or `buildagent.json` at the project root is
    discovered; a file of another name, or in another directory, is not.
  - S6.2 Two matching files at the root fail with `MultipleConfigurationFiles` naming every path
    found — never a precedence decision between them. `.yml` and `.yaml` count as two files, not one
    format spelled two ways.
  - S6.3 A missing `schemaVersion` fails with `SchemaVersionMissing` and the message names the
    supported versions.
  - S6.4 A `schemaVersion` outside the supported set fails with `SchemaVersionUnsupported`;
    `schemaVersion` is an integer and a non-integer value fails.
  - S6.5 A key under `parameters` with no build parameter fails with `UnknownKey`, naming the key
    and the nearest known key.
  - S6.6 A key is accepted in exactly one spelling — the kebab-case flag name without `--`. The
    camelCase spelling of the same parameter fails with `UnknownKey`.
  - S6.7 A secret-declared parameter present in the file fails with `SecretKeyRejected`, naming the
    key and the tiers that may supply it, and its value appears in no output stream.
  - S6.8 A value that cannot convert to the parameter's declared type fails with `ValueTypeMismatch`
    naming the key, the declared type and the value's shape — never the value itself (I25).
  - S6.9 A `buildType` that is absent or outside the five fails with `UnknownBuildType` naming the
    accepted five.
  - S6.10 A non-nullable parameter with no value at any tier fails with `RequiredValueMissing`
    naming the key and the tiers consulted.
  - S6.11 A file that is not well-formed, or that uses a construct with no JSON equivalent, fails
    with `MalformedDocument` naming the path and position.
  - S6.12 A file that exists but cannot be opened fails with `FileUnreadable`.
  - S6.13 A file containing four distinct errors reports all four in one pass, before any build step
    runs (I18).
  - S6.14 Every error above exits `2` and runs no build step.
  - S6.15 Resolution creates and modifies no file: the resolved configuration is never written to
    disk.
  - S6.16 The project file's bytes are identical before and after a build, including a build that
    fails validation — the product never migrates or rewrites a consumer's file.
  - S6.17 Automated tests cover, and report the count of, an unknown key, both formats present, a
    missing schema version, an unsupported schema version, and a malformed file.
Withdrawn: the criterion requiring the `node-template` flow to call this validator is withdrawn —
how PowerShell reaches Config across the language boundary is `## Unresolved` U-5, and each of the
three candidate answers is a different public surface.
Out of scope: precedence between configuration sources (S7); the sample files (S8); removing or
deprecating any configuration surface that exists today.

## S7 — One predictable precedence across every source
Delivers: Someone who sets the same setting in two places can tell which one wins without
experimenting, and can see for every effective value which source it came from.
Touches: Config's merge and its resolved-value and tier types; mapping-file expansion; generated
environment files.
Depends on: S6.
Acceptance:
  - S7.1 For a key set in every tier, the effective value is the invocation argument, and its
    recorded tier says so.
  - S7.2 Removing each tier in turn from that case yields the next tier down in the contract's
    order, with the recorded tier matching at every step.
  - S7.3 Every resolved value records the tier it came from; none is recorded as unknown.
  - S7.4 `BuildAgentConfig.Parameters` resolves as tier 2 and `Invoke-Build -args` as tier 1, which
    is R-INVOKE-002 expressed in the contract's tiers.
  - S7.5 A map-derived value does not overwrite a variable already set in the process environment.
  - S7.6 Generated environment files are removed after a successful build, after a failed build, and
    when the process is terminated by a signal.
  - S7.7 A secret-declared parameter is redacted in every output stream whichever tier supplied it,
    and redaction covers every secret-declared parameter rather than values matching a token-shaped
    pattern (I25).
  - S7.8 Config exposes no overload or flag that returns on the first error, and no member that
    writes the resolved configuration.
  - S7.9 Nothing in the resolution request reorders the tiers.
  - S7.10 The accepted key set is in bijection with the build parameter vocabulary: adding a
    parameter makes its kebab-case key accepted, and no accepted key exists without a matching
    parameter (I17).
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
  - S8.3 Each sample declares an integer `schemaVersion` in the supported set and a `buildType` from
    the five.
  - S8.4 Every key in every sample is in the accepted kebab-case spelling.
  - S8.5 No sample contains a secret-declared parameter or a placeholder credential.
  - S8.6 The YAML and the JSON sample for a build type express the same settings.
  - S8.7 A sample that stops validating fails the build, so a parameter change cannot silently
    invalidate the samples.
Out of scope: documenting the samples on the published site (S11); adding parameters only to make a
sample look richer.

## S9 — The PowerShell module runs its own image version
Delivers: Someone who pins a module version now gets the build image that matches it, so a pinned
setup stops changing underneath them whenever a new image is published. The old behaviour is called
out as a breaking change with a migration step.
Touches: the module's default configuration; `PSModule.requirements.md`; the module's tests; the
2.0.0 release notes and migration guide.
Depends on: S3.
Acceptance:
  - S9.1 With no image configured, the module's effective `DockerImage` is the module's own version,
    not `latest`.
  - S9.2 The reference stays overridable through `Set-BuildAgentConfig`, and an override is used
    verbatim.
  - S9.3 `PSModule.requirements.md` R-CONFIG-002 states the version-pinned default, and no document
    still states `latest` as the module's default.
  - S9.4 The change appears in the 2.0.0 release notes' breaking-changes section and in the
    migration guide.
  - S9.5 The module's tests pass on Windows PowerShell 5.1 and on PowerShell 7, and both are release
    gates rather than best effort.
  - S9.6 A configured image that cannot be obtained fails with `ImageUnavailable` and exit `5`, with
    no substitution of another version (I27).
  - S9.7 A workspace path that is absent or not a directory fails with `WorkspaceInvalid` and
    exit `3`.
  - S9.8 A non-zero container exit is propagated unchanged rather than remapped (I28).
  - S9.9 The exported set remains exactly `Set-BuildAgentConfig`, `Invoke-Build` and
    `BuildAgentConfig`, and each exported name and parameter is a manifest item.
Out of scope: publishing the module to the PowerShell Gallery, which is blocked on key custody; any
other module default; the global tool's launcher.

## S10 — Documentation cannot name what the product does not have
Delivers: A reader of the documentation site, the README or the PowerShell help can trust that every
command, option and path named there actually exists, because a change introducing one that does not
is stopped before it merges.
Touches: the docs check and its error type; the surface manifest it reads; the pull-request workflow.
Depends on: S1.
Acceptance:
  - S10.1 The check runs on every pull request and fails it on a violation.
  - S10.2 A documented path, command, parameter, build type or discovery location the tree and
    manifest lack fails with `UnknownName`, naming the document and the name (I45).
  - S10.3 A document covering a protected surface that names no canonical contract fails with
    `CanonicalSourceMissing` (I46).
  - S10.4 Two documents claiming to be canonical for one surface fail with
    `CanonicalSourceConflict`.
  - S10.5 The check covers the documentation site sources, the README and the PowerShell help.
  - S10.6 A surface that is deprecated but still present does not fail the check.
  - S10.7 The check's output states which classes of claim it does not cover, and it makes no claim
    about behavioural statements.
  - S10.8 Run against the tree as it stands when the slice starts, the check reports its findings
    rather than being tuned to pass; each finding is either fixed or recorded.
  - S10.9 The check does not read `docs-template/`, which is pinned and not owned here.
Out of scope: rewriting documentation prose (S11); verifying behavioural claims; changing
`docs-template/`.

## S11 — One page that states the promise, and a way to move to 2.0.0
Delivers: A public user can read in one place what is protected, how versions work, which versions
get fixes, how deprecation works, which hosts and CI are supported, and the Docker socket warning.
Someone on 1.x can follow a guide that covers every breaking change and the move from a floating tag
to a pinned version.
Touches: the published documentation site; the compatibility page; the deprecation policy; the
migration guide; the release-notes template.
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
  - S11.7 The page states that update behaviour is verified on Linux and that Windows live-daemon
    coverage is a gap, rather than describing it as verified.
  - S11.8 The page passes the S10 check.
Out of scope: classifying the rest of the documentation (S12); writing per-build-type reference
documentation.

## S12 — Every document about a public surface has one owner
Delivers: Anyone reading about a public surface lands on the one document that defines it, because
every other document that covered the same ground now either points at it or is gone.
Touches: the README; the product instructions; the published documentation sources; the PowerShell
documents; the historical design reports.
Depends on: S10, S11.
Acceptance:
  - S12.1 Every document overlapping a public surface is classified as a canonical contract, a
    reference naming its canonical source, or removed, and the classification is recorded in one
    list.
  - S12.2 The `build` command, project configuration, Docker template discovery, the global tool and
    the PowerShell module each have exactly one canonical contract, and `PSModule.requirements.md`
    is the module's.
  - S12.3 Every reference document names the canonical contract it explains, so the check reports no
    `CanonicalSourceMissing`.
  - S12.4 No surface has two documents claiming to be canonical, so the check reports no
    `CanonicalSourceConflict`.
  - S12.5 Removed documents are gone from the published site and remain in git history only.
  - S12.6 No document still disagrees with the tree on a name the S10 check covers.
Out of scope: rewriting reference prose beyond adding the canonical pointer; changing
`docs-template/`.

## Landed

*None yet.*

## Blocked

Each item names a contract `## Unresolved` entry. None may be answered by an implementing slice.

**The global tool's published entry point — issue #14 (U-1).** Naming the .NET package id or the
invoked command would introduce a signature the contract does not carry. This blocks installing the
tool from the public .NET tool feed, its `ToolCommand` manifest items, and its published invocation
in every document. It does not block S2, S4 or S5, which are written against the Updater and the
command *surface*, which the contract determines regardless of the name. The same entry's key-custody
half blocks publishing the PowerShell module to the Gallery, although the module's name is settled.

**The `node-template` flow's route to Config (U-5).** In-process, a tool subcommand, and a
JSON-emitting invocation are three different public surfaces. Until one is chosen, no slice can land
the single-validator invariant across the language boundary, and S6 deliberately omits that
criterion.

**Resume safety and claim atomicity (U-6, U-7).** Nothing in a claim proves an existing sink
artifact came from that claim, and the check-then-create window permits two drafts. S3's resume and
`latest`-ordering criteria are withdrawn rather than written against an assumed answer.

**The `latest` move's position in the publish order (U-9).** `10-design.md` § Control flow 2 moves
`latest` before the last fallible step, which contradicts I4. One of the two has to give, and
that is an adjudication, not a slice.

**The Updater's dependency on Notifications (U-8).** The tool is declared to share no Build-types
code while Notifications is declared to live inside Build types. S4's notification criterion is
withdrawn until the boundary is settled.

**The unsupported container shape list (U-10).** Beyond anonymous volumes and legacy container
links, the list is whatever the Docker API fails to reproduce from inspect output — a verified fact
about the daemon. S2 runs the probe and records the results; extending the refusal list is a
separate adjudication.

**Recovering prior-container residue.** The contract's tool surface has `update --clear-lock` and no
command that returns a prior container left behind by an interrupted update. An operator can
therefore clear the lock but has no supplied way to complete the restore, and S5's criteria for it
are withdrawn. This needs a contract amendment before a slice can land it — it is a gap in the
contract rather than an open question already recorded in it.

---

Run `/track` next to open issues from these slices. This document opens none.
