# Design — Docker-BuildAgent

Input: [`00-brief.md`](00-brief.md). Decisions made here are logged in [`90-decisions.md`](90-decisions.md); this document gives the architecture they add up to. Parameter names, config keys, exit-code values and message texts belong to `20-contract.md`, not here. Anything the tree already declares (Params classes, workflow files, the module manifest) is pointed at, not copied.

## Data model

### Release version

- **Identity:** a SemVer 2 string, without the `v` prefix. The git tag and release tag add the prefix.
- **Derived from:** GitVersion over the commit being released, unless a CI dispatch supplies a version. A supplied version passes the same validation and the same existence check as a computed one. No step skips the check.
- **One value per release:** CI computes it once and stamps it into all three artifacts: the image's versioned tag, the global tool package, and the PowerShell module. A version already in the tree (for example the module manifest's) is overwritten at release and is never authoritative. The stamping is what makes "one shared version" true by construction, not by convention.
- **Constraint:** the major must not be lower than the highest released major. Fixes ship only in the current major, so a release against an older major is refused.
- **Persisted:** as the git tag, the GitHub release, and the version on each of the three sinks.

### Release claim

The record that settles whether a version exists. It exists so the check does not rest on any single sink enforcing it.

- **Identity:** the version.
- **Fields:** the version, the commit SHA it was claimed for, state (claimed or published), the assembled release notes, and the surface manifest (below).
- **Persisted as:** a GitHub release. It is a draft while claimed and becomes a normal release when published. Nothing else is persisted, and nothing is held in memory across runs.
- **Lifecycle:** claimed before any sink is written, then published after every sink holds the version.
  - A draft for the same SHA is a resumable claim.
  - A draft for another SHA, or a published release, means the version exists.
- **Derived:** "the version exists" is true when any of these holds:
  - a published release exists;
  - a draft exists for a different SHA;
  - any sink holds the version without a matching claim.

  Sinks are read, not trusted to refuse.

### Surface manifest

A machine-readable description of every protected surface that can be compared mechanically.

- **What it records, for each item:** a name, a comparable value, and a deprecated flag with the version it was deprecated in. For a parameter or a config key the value is its type and default; for every other item it is whatever that surface declares — an entrypoint, a mount point, a position in an ordered list. **An item whose value cannot be recorded is not in the manifest**, because the gate fails on any difference and cannot fail on what it does not hold.
- **What it covers:**
  - build types, and the parameters and defaults of each;
  - project configuration keys and supported schema versions;
  - global tool commands and their parameters;
  - PowerShell module exported commands and their parameters;
  - the ordered list of Docker-template discovery locations;
  - image invocation inputs: entrypoint, `build` command, mount points, and the environment variables it reads.
- **Derived from:** the declarations that define each surface: Params classes, the config schema, module exports, and the image definition. It is never hand-written. The module's existing generated parameter file is the precedent, and is one projection of this manifest.
- **Persisted as:** an asset of each release, so that the next release's baseline is whatever was actually shipped, not a copy that could have been edited.
- **What it cannot record:** behaviour. Documented-behaviour changes are not in it, and stay a release-notes duty.

### Project configuration file

- **Identity:** one file at the project root, with a fixed base name and a YAML or JSON extension.
- **Fields:**
  - a required schema version;
  - parameter values whose keys are the same parameter vocabulary the manifest records.

  A key exists if and only if a build parameter exists.
- **Schema version:** independent of the product version. It is bumped only when the file's own shape changes incompatibly. Each product release supports a declared set of schema versions, and that set is itself a manifest item.
- **Secrets not accepted:** parameters marked as secrets are rejected in the file. A project file is committed and copied into build contexts, and the brief forbids secrets reaching images or logs.
- **Persisted:** consumer-owned and read-only to the product.
- **Resolved configuration:** the in-memory result of merging every source, with each value's source recorded. It is never written to disk. Secret values stay redacted wherever it is displayed.

### Configuration precedence

Highest first. Each tier shadows everything below it.

1. **Invocation arguments**, including arguments the global tool or module passes through on behalf of their caller.
2. **Module configuration** (`Set-BuildAgentConfig` parameters). It arrives as arguments at the container boundary, but ranks below the caller's explicit arguments, which is the module's existing merge rule.
3. **Process environment**, including values `set-environment.ps1` sets.
4. **Map-derived environment:** values generated from the build map file. Today this file is loaded over the process environment. From 2.0.0 it no longer overwrites an already-set variable, and the migration guide records that change.
5. **Project configuration file.**
6. **Declared defaults.**

The app env map file is not a configuration source. It generates an output of the build (the built app's environment) and draws its values from the resolved configuration and environment. It is therefore outside the precedence order.

### Container update record

- **Identity:** a generated update id.
- **Fields:**
  - the container name and container id;
  - the prior image id and the requested image reference;
  - the new image id;
  - the prior container's retained name;
  - the options in effect: health timeout and restore on or off;
  - start and end times;
  - the outcome.
- **Excluded:** container environment, commands and mounts. Inspect output carries consumer secrets.
- **Persisted:** as append-only log entries in a per-user state location on the machine running the command. The start entry is written and flushed before the first change, and the outcome entry after the last. A start without an outcome is the durable evidence that an update was interrupted.

### Update lock

- **Identity:** a created-but-never-started container whose name is derived deterministically from the **target container's name**, hashed so that any name an operator may pass yields a valid container name. The Docker daemon enforces name uniqueness atomically, so the lock is a mutex at the daemon. It holds across every client of that daemon, not only one machine's processes.
  - The key is the name, not the container id, because the id under that name **changes inside the critical section**: the update renames the original away and creates the replacement under the same name (*Control flow* → *An operator updates a named container*, step 8). A lock keyed on the id would stop naming the same thing part-way through the operation it guards, and a second update resolving the name during the health wait would derive a different lock and acquire it. The brief's invariant is written over the name an operator passes, so the lock is too.
- **Fields:** carried as labels: owner machine, process, update id, acquisition time, and a deadline (acquisition time plus the stop grace period, the health timeout, and a fixed margin — the phases that remain inside the section once the pull is outside it).
  - The deadline is **diagnostic, not an authorization**. It is how a refusal words itself, never a licence to act. A clock skew therefore changes only how a message reads, never whether two processes can mutate one container.
- **Image:** the target's current image, which is always present locally. Taking the lock therefore never pulls.
- **Lifecycle:** created at step 4 and removed at update end on every path.
  - **A lock is never taken over automatically.** A lock past its deadline is reported — owner machine, process, age, and how far past — and the update refuses. Removing a lock does not stop the process that holds it, so a takeover would put two owners inside a section designed for one; the owner re-reading its own lock cannot close that gap either, because Docker offers no fencing token to make the check and the next container operation one act.
  - Refusing costs nothing that was previously recovered automatically. A crash from step 6 onward strands a pin, an outcome-less start entry, or a prior container, and *Failure modes* → *Process interruption* already recovers those through the residue refusal rather than through the lock. The only window a takeover ever covered was a crash during the pull, and the pull is now outside the lock.
  - Clearing the lock of a dead update is an operator action, reported by the refusal and specified as a contract item — the same posture this design already takes for completing a restore from residue.
- **Persisted:** in the daemon.

### Prior image pin and prior container

- **Prior image pin:** a product-owned tag per target container, pointing at the prior image id. Exactly one pin per container is kept; the next successful update replaces it. The pin keeps the prior image from being pruned after the prior container is removed.
- **Prior container:** the target container itself, stopped and renamed to a product-owned name for the duration of an update.
  - Restore renames it back and starts it. Restore is therefore exact: nothing is recreated from inspect output.
  - It is removed when an update ends in success, or ends unhealthy with restore disabled.
  - A labelled prior container is **residue** of an interrupted update when the lock for the name it was renamed from is absent, or is present and past its deadline. That test is sound only because the lock is keyed on the name: the lock outlives the rename, so its presence is what separates an update still running from one that died. The update log cannot make that distinction — it is per-user and per-machine, so another client of the same daemon cannot read it, and a start without an outcome reads identically in both cases.

## Module boundaries

| Module | Owns | Depends on | Exposes |
|---|---|---|---|
| **Surface model** | Derivation of the surface manifest from declarations; manifest comparison rules (removal, rename, changed default, removal of a non-deprecated item) | Declarations in Build types, Config | The manifest and a comparison result |
| **Config** | Schema per schema version, discovery of the project file, validation, precedence merge, and source attribution | — (reads declarations it is given) | A resolved configuration, or a validation failure listing every error found |
| **Build types** | The five build types (Forge/NUKE and the node-template script flow), Docker-template discovery, image build and push, and notifications | Config, Docker CLI, registry, GitHub API, external template repository | `build <type>` inside the image |
| **Image** | The runtime environment and bundled tools, and the `build` entry that dispatches to Build types | Build types | The image invocation surface |
| **Launchers:** global tool and PowerShell module | Translating a host invocation into one image invocation (mounts, environment passthrough, Docker host, arguments), and pinning the image version | Image (by version, as a black box), Docker on the host | `build <type>` on the host. The module also keeps its own contract in `PSModule.requirements.md` |
| **Updater** (inside the global tool) | Container update, lock, prior pin, restore, update log, and outcome status | Docker daemon API, Notifications (optional) | The update command |
| **Release pipeline** | Version computation, claim, stamping, release-notes assembly and section gate, surface comparison gate, and publishing to all sinks | Surface model, Build types (to build the image), GitHub API, registry, .NET tool feed, PowerShell Gallery | CI workflows; no consumer surface |
| **Docs check** | Checking published docs, README and module help against the surface manifest and the tree on every change | Surface model | A pass/fail gate in PR CI |

The dependencies run one way:

```
Docs check ───────► Surface model ──► Config
Release pipeline ─► Surface model
Release pipeline ─► Build types ────► Config
Image ────────────► Build types
Launchers ········► Image            (by published version, not by code)
Updater ──────────► Notifications    (shared service within Build types)
```

- **Acyclic:** Config depends on nothing in the product. The Surface model reads declarations without executing builds. Nothing depends on the Release pipeline, the Docs check, the Launchers or the Updater.
- **Launchers:** the launch path depends on the Image only through its published invocation surface and shares no code with Build types.
- **Updater:** its dependency on Notifications is on the shared notification service alone, not on any build type.
- **node-template:** the script flow resolves configuration by calling the Config module, not by reimplementing it, so there is exactly one validation contract.

## Control flow

### 1. A consumer runs `build <type>` (in the image, or on the host through the global tool or module)

1. **On the host (launchers only):**
   - The launcher resolves the image reference: its own version, unless overridden (see Open questions for the module's default).
   - It resolves the Docker host and forwards the explicit arguments. For the module, it also forwards module configuration at its own precedence tier.
   - It runs one container with the workspace mounted, and returns the container's exit status unchanged.
2. **In the image:** `build` validates the type against the accepted set, and Build types starts the requested type.
3. **Config:**
   - locates the project file (both formats present is an error);
   - parses it, checks its schema version against the supported set, and rejects unknown keys and secret keys;
   - merges all sources in precedence order.

   Every error is reported together, before any build step runs.
4. **Deprecation warnings:** using a deprecated item (parameter, key, command or build type) emits a warning naming the item and the version it will be removed in. The build continues.
5. **The build runs:**
   - For image-producing types, template discovery walks the manifest's ordered locations when no Dockerfile exists.
   - Push and GitHub publishing happen only under the existing conditions (forced, or non-local and not a dry run).
6. **Notification** is attempted when configured. Generated environment files are removed on every exit path.

### 2. A maintainer releases a version (CI dispatch, a version tag push, or a main push; see Open questions for main)

Steps 1–5 write nothing. The first write is step 6.

1. Run tests. Compute or validate the version.
2. **Refuse** if the version exists (derived rule in *Release claim*), if its major is below the current major, or if a version tag already points at a different commit.
3. Derive the surface manifest. Load the baseline, which is the manifest asset of the highest published release below this version.
   - **Fail** on *any* difference from the baseline unless the major increases, except the compatible set: adding an item, marking an existing item deprecated, and adding a supported schema version.
   - **Fail** on removal of an item the baseline did not already mark deprecated, at any major.
   - v2.0.0 has no baseline and is judged only by the migration guide.

   The rule is a whitelist so that it cannot fall behind the manifest: a list of forbidden differences leaves each field later added to the manifest unchecked, and reports nothing when it does.
4. Assemble release notes. Mechanically detected breaking changes and deprecations are inserted into their sections. **Fail** if either section heading is missing from the final body. An empty section carries an explicit "none".
5. Build every artifact with the version stamped in. Nothing is pushed yet.
6. **Claim:** create the draft release bound to the commit SHA, with notes and manifest attached.
7. Publish to the sinks in a fixed order: image versioned tag, global tool, PowerShell module, then the movable `latest`. A sink that already holds this version **under this claim** is skipped, not rebuilt; this is what makes a retry resume.
8. Create the git tag if absent, then publish the release. The docs-site build dispatch follows.

### 3. An operator updates a named container (global tool)

Steps 1–3 change nothing and take no lock. Step 4 creates the lock and step 5 checks; the first change to the target happens at step 6.

1. Resolve the name to a container id and inspect it.
2. **Refuse** on either of two distinct grounds. They fail for different reasons and are two rules, not one list.
   - The prior configuration cannot be **restored** exactly: the container is auto-removed on stop, or it is managed by an orchestrator.
   - The replacement cannot be **created faithfully** from the target's inspected configuration, because the target uses configuration that inspect output does not round-trip — anonymous volumes, links, and some network and runtime options. Which shapes are supported and which refuse is a contract item; this document states the rule, not the list.
3. Pull the requested image. This happens **before** the lock is taken, so the one unbounded phase of the update is outside the critical section and a slow pull can never be mistaken for a dead owner.
   - A pull failure ends the update with nothing changed and no lock taken.
   - A new image id equal to the prior one ends with a logged no-op success.
   - Two updates of the same container may both reach this step. The pull mutates no shared state, so the loser of the race has wasted work and nothing more.
4. Acquire the lock. **Refuse** if a lock exists. A lock past its deadline is reported as a probable dead update, not taken over.
5. **Refuse** if there is residue of an interrupted update, reporting the residue and its log entry.
6. Pin the prior image. **Refuse** if the pin cannot be created ("the prior image cannot be kept").
7. Write and flush the start entry. **Refuse**, and remove the new pin, if it cannot be written.
8. Stop the target and rename it to its prior-container name. Create a new container with the original name, the new image and the inspected configuration, then start it.
   - **The replacement differs from the target in image only.** Inspect output is the only available source for the rest, and it is lossy (*Alternatives considered* → *Preserving and restoring the prior version*), so this invariant is held by the creation refusal at step 2 — not by anything observed here or at step 9.
9. Wait for the new container's own health status until the timeout:
   - `healthy` → **success**;
   - the container is not running and will not be restarted → **fail**;
   - no declared health check → **fail**;
   - deadline reached → **fail**.

   An `unhealthy` status does not end the wait early, because a check may recover within its own retry window.
10. **On success:** remove the prior container, replace the pin, write the outcome entry, release the lock, and exit with success.
11. **On failure with restore on:**
    - remove the new container, rename the prior container back, and start it;
    - write the outcome entry and release the lock;
    - exit with the "restored" status.

    The restored container's health is reported, not re-gated.
12. **On failure with restore off:** leave the new container running, remove the prior container, keep the pin, write the outcome entry, release the lock, and exit with the "unhealthy, not restored" status.
13. **Notification:** optional, sent after the outcome is logged. Its failure never changes the exit status.

## Failure modes

### Build path

**Project configuration**

- **What fails:** a malformed file, both formats present, a missing or unsupported schema version, an unknown key, or a secret key.
- **Detection:** Config validation before any build step.
- **System response:** abort with every error listed. Nothing is built, and no environment file is generated.
- **User sees:** a non-zero exit, with each error naming the file, the key and the rule.
- **State left behind:** none.

**Environment and map files**

- **What fails:** a map-file entry has an empty value.
- **Detection:** env file generation.
- **System response:** abort, as today.
- **User sees:** the name of the unresolved key.
- **State left behind:** the partially generated env file is removed on exit.

**Deprecated input**

- **Detection:** at config resolution.
- **System response:** warn and continue.
- **User sees:** the item and the version it will be removed in.

**Docker CLI or daemon, including the socket**

- **What fails:** the daemon is unreachable or permission is denied.
- **Detection:** the first Docker call.
- **System response:** abort the build.
- **User sees:** the Docker error, plus the Docker host in use. Credentials in the host URL are stripped.
- **State left behind:** local images may be partially built.
- **Retry:** re-running is safe, because builds are isolated.

**Registry**

- **What fails:** pull rate limits (anonymous), push authentication, or a network or proxy failure.
- **Detection:** the Docker exit status.
- **System response:** abort. The build path never retries automatically.
- **User sees:** the registry error with tokens redacted.
- **State left behind:**
  - **Build path:** `latest` and the versioned tag may diverge on a push-order failure. This is acceptable only because the build path is not a release; the release path's ordering and claim govern published versions.

**External template repository (node-template)**

- **What fails:** the clone fails, or the branch is missing.
- **Detection:** the git exit status.
- **System response:** abort before copying anything.
- **User sees:** the repository and branch.
- **State left behind:** the workspace is unchanged. Copies never overwrite.
- **Compatibility:** the fetched template content is not a protected surface. It is external, and is not named by the contract.

**Docker-template discovery**

- **What fails:** no Dockerfile, and no template for the detected app type in any location.
- **Detection:** discovery.
- **System response:** abort.
- **User sees:** every location searched, in order.

**Secrets**

- **What fails:** a secret reaches a log or an image.
- **Detection:**
  - **Logs:** the redaction pass on all display output, extended to every parameter marked secret, not only token-shaped patterns.
  - **Images:** secrets are rejected in the project file, and generated env files are removed before a build context is sent.
- **Residual risk:** a consumer's own Dockerfile copying secrets it was handed is consumer code. The documentation says so.

**Launchers (global tool, module)**

- **What fails:** Docker is missing on the host, the image version cannot be pulled, or the workspace path cannot be mounted (Docker Desktop file sharing).
- **Detection:** the docker exit status before the build starts.
- **System response:** the launcher reports and exits non-zero. It never falls back to `latest` or to any other version.
- **User sees:** the image reference, and whether the failure was the pull or the mount.

### Release path

**Version already exists**

- **Detection:** the claim check reads the GitHub releases and every sink before any write.
- **System response:** refuse. This applies equally to computed, dispatched and tag-triggered versions.
- **User sees:** which record shows the version exists.
- **State left behind:** none.

**Surface comparison gate**

- **What fails:** a manifest difference step 3 does not place in the compatible set, or a removal without prior deprecation.
- **Detection:** manifest diff.
- **System response:** fail before the claim.
- **User sees:** each differing item, what changed about it, and the rule it broke or the absence of one that admits it.

**Release notes gate**

- **What fails:** a required section is missing.
- **Detection:** the notes check.
- **System response:** fail before the claim.

**Partial publish**

- **What fails:** one sink fails after earlier sinks succeeded.
- **Detection:** the sink step's exit status.
- **System response:** stop. The claim stays a draft.
- **State left behind:** a draft release plus some sinks holding the version. Nothing is deleted, because tags are immutable and published packages cannot be withdrawn cleanly.
- **Retry semantics:** a re-run for the **same commit** resumes: sinks already holding the version under this claim are skipped, and the rest are published. A re-run from a different commit is refused. Moving `latest` last means a partial release never moves `latest`.

**GitHub API**

- **What fails:** the API is unavailable or permission is insufficient.
- **Detection:** the API response.
- **System response:** before the claim, fail with nothing written. After the claim, same as partial publish.

**.NET tool feed and PowerShell Gallery**

- **What fails:** a key is missing, the feed rejects a duplicate, or indexing is delayed.
- **Detection:** push response. A version-visibility check runs as part of the existence check.
- **Consequence:** a feed rejecting a duplicate is a second line of defence, not the check. An indexing delay can make a just-published package invisible to a re-run's existence check; the claim, not feed visibility, is what the resume logic keys on.

**Git tag**

- **What fails:** the tag exists on a different commit, or tag creation fails.
- **Detection:** check before the claim; tag creation after the sinks.
- **System response:** refuse before the claim. A failure after the sinks leaves the draft, and a re-run resumes at tag creation.

### Update path

**Target container**

- **What fails:** it does not exist, is auto-remove, is orchestrator-managed, or carries configuration the replacement cannot be created faithfully from.
- **Detection:** inspect.
- **System response:** refuse.
- **State left behind:** none.
- **Why this is a refusal and not a check after the fact:** step 9 gates on the new container's own health, and a container that lost an anonymous volume can be entirely healthy. Health is not evidence of configuration fidelity. Success then removes the prior container, so the loss would be both undetected and unrecoverable — which is why the only place to catch it is before anything changes.

**Lock**

- **What fails:** a lock is held, whether by a running update or by one that died holding it.
- **Detection:** lock creation fails on the name conflict; the deadline label is read to word the message.
- **System response:** refuse, in both cases. The design cannot distinguish a dead owner from a slow one and does not guess.
- **User sees:** the lock's owner machine, process and age; past its deadline, that it is probably a dead update, and the operator command that clears it.
- **State left behind:** none.

**Residue**

- **What fails:** a labelled prior container exists, or a log start entry has no outcome.
- **Detection:** inspection at start.
- **System response:** refuse before changing anything.
- **User sees:** the residue and the interrupted update's id. The operator's way to complete the restore is a contract item.

**Registry pull**

- **What fails:** auth, rate limit, network, or an unknown reference.
- **Detection:** pull status.
- **System response:** end the update with nothing changed, and log the outcome.
- **State left behind:** none, and no lock — the pull runs at step 3, before the lock is taken. This is why the update's slowest and least predictable step cannot strand one.

**Prior image pin**

- **What fails:** the tag operation fails.
- **Detection:** the Docker API.
- **System response:** refuse ("prior image cannot be kept").
- **State left behind:** none.

**Update log**

- **What fails:** the state location is unwritable or the disk is full.
- **Detection:** write and flush.
- **System response:**
  - at the start entry → refuse, and remove the pin;
  - at the outcome entry → the outcome still decides the exit status, and stderr says that the log write failed. The stranded start entry is then treated as residue on the next run.

**Stop, rename or create**

- **What fails:** the Docker API fails between the stop and the new container starting.
- **Detection:** API status.
- **System response:** treat as a failed update and take the restore path. Restore runs even when restore is disabled, because no updated container exists to keep.
- **User sees:** the failing step and the restore outcome.

**Health wait**

- **What fails:** unhealthy until the deadline, the container exits, or no health check is declared.
- **Detection:** polling health status and state.
- **System response:** as in the *Control flow* update path, steps 9–12.

**Restore**

- **What fails:** removing the new container, renaming back, or starting the prior container fails.
- **Detection:** API status.
- **System response:** stop. Write an outcome entry of "restore failed" and release the lock. The prior container remains as residue, so the next update refuses rather than acting on broken state.
- **User sees:** a distinct "restore failed" exit status, and the name the prior container is left under.

**Process interruption**

- **What fails:** a signal or crash.
- **Detection:**
  - **Signal:** a handler. Before step 8 it releases the lock and removes the pin. After step 8 it runs the restore path.
  - **Crash:** no in-process detection. A crash before step 4 leaves nothing at all, because the pull is outside the lock. From step 4 the lock remains, and from step 6 a pin, an outcome-less start entry, or a prior container remains with it.
- **Outcome:** the next update refuses — on the lock, reporting it as past its deadline, and on whatever residue accompanies it. Both are cleared by the operator; neither is reclaimed automatically.

**Notification**

- **What fails:** the webhook is unreachable.
- **Detection:** HTTP status.
- **System response:** a warning. The exit status is unchanged, and the webhook URL never appears in output.

### Docs check

- **What fails:** a doc names a command, parameter, build type, discovery location, repository path or linked tree file that the manifest or tree lacks.
- **Detection:** extraction against the manifest and the tree, in PR CI.
- **System response:** the PR check fails, listing the document, line and unknown name.
- **Coverage:** behavioural claims are not mechanically checkable and remain review scope. The check does not claim to cover them.

## Concurrency and ordering

- **Build invocations:** these may run concurrently without limit. Isolation is the enforcement: each invocation reads its own workspace and environment, and generated env files live in the workspace. Two builds of the **same** workspace at once are unsupported. The brief places shared mutable build state out of scope, and nothing detects this case.
- **Releases** must not run concurrently with each other, whatever the trigger. Two mechanisms enforce this:
  1. **Primary:** a single CI concurrency group spanning every workflow that publishes, queued rather than cancelled, so a started release is never killed mid-publish.
  2. **Backstop:** the claim check, which catches any concurrent claimant that bypassed the group, such as a mistakenly added workflow.
- **Claim race:** the claim check and the claim creation are separate API calls, so the backstop has a window in which two drafts can exist. The concurrency group is what closes it.
- **Release ordering:** the order is fixed and every step before the claim is read-only:
  1. existence check;
  2. surface gate;
  3. notes gate;
  4. build;
  5. claim;
  6. sinks, with `latest` last;
  7. git tag;
  8. publish the release.

  This order means a failure before the claim leaves nothing behind, and a failure after it is resumable.
- **Container updates:** updates of **different** containers may run concurrently; each takes its own lock. Updates of the **same** container must not — same meaning the same container *name*, which is the identity the operator supplies and the identity the lock is keyed on, so the lock holds for the whole update even across step 8's rename and re-create. The daemon-side lock enforces this across processes and across machines using the same daemon. The section the lock covers is bounded, because the registry pull sits outside it. A lock is never taken over while it exists, so there is no path by which two processes both believe they hold it; a past-deadline lock is evidence for an operator, not permission for the next update.
- **Update steps:** strictly sequential within one update. The start entry is flushed before the first change, and the lock is released only after the outcome entry is written.
- **Launchers and image version:** a launcher pins one image version per invocation. Concurrent invocations with different launcher versions run different images, which is safe because image versions are immutable.

## Alternatives considered

1. **How the global tool reaches the build types.**
   - **Chosen:** a launcher that runs the versioned image. This is the same model as the PowerShell module.
   - **Rejected:** running Forge in-process on the host, as issue #14 proposed. Every build type would need the image's full toolchain on Windows and Linux hosts (SDKs, node and pnpm, GitVersion, PowerShell). The container-internal template fallback and the script-only node-template flow would need host equivalents. The supported environment matrix would double for no user-visible gain, and the brief explicitly leaves internal reach unspecified.
   - **Also rejected:** the module invoking the tool. The module would then require a .NET tool install, which its PowerShell 5.1-only requirement does not state.
2. **Where "version exists" is decided.**
   - **Chosen:** a draft-release claim bound to a commit, checked against every sink before any write.
   - **Rejected:** git tag existence alone. The tag-triggered workflow starts from an already-existing tag, and a tag says nothing about which sinks hold the version.
   - **Rejected:** relying on registry and feed duplicate rejection. The brief forbids depending on the registry, and GHCR overwrites tags freely.
   - **Rejected:** writing the tag or release first with no draft state. A partial publish would then look exactly like a complete release and could never be resumed.
3. **Update lock location.**
   - **Chosen:** a named, never-started container at the daemon.
   - **Rejected:** a lock file on the client machine. It does not exclude a second machine driving the same daemon, and Windows and Linux lock semantics differ.
   - **Rejected:** a named volume. Volume creation with an existing name succeeds, so it is not a mutex.
   - **Rejected:** a label on the target container. Labels cannot change after creation.
   - **Rejected:** deriving the lock name from the target's container id. The id under the target name changes at step 8, so the lock would stop identifying the update's own target half-way through the section it protects.
   - **Rejected:** taking over a lock past its deadline. It excludes other takers but not the owner, which no removal can stop; and once the pull moves outside the lock, every crash it would have recovered is already recovered by the residue refusal.
4. **Preserving and restoring the prior version.**
   - **Chosen:** stop and rename the original container, pin its image, and restore by renaming it back.
   - **Rejected:** removing the original and re-creating it from inspect output on restore. Faithful re-creation from inspect output is lossy (anonymous volumes, links, some network and runtime options), so restore would be approximate on exactly the path that has to work.
   - The **forward** path has no such alternative: creating the replacement can only read the target's inspected configuration. The same lossiness therefore applies to it, and is handled by **refusing** at step 2 rather than by tolerating an approximation. Restore stays exact because it renames rather than re-creates; creation is made safe by never being attempted on a shape it cannot reproduce.
   - **Rejected:** tagging only (issue #1's `:previous`). A single shared tag name collides across containers using one repository, and it keeps the image but not the container configuration.
   - **Rejected:** comparing the new container's inspect output against the original's before removing the prior container. The comparison set is large and moves with every Docker version, and a new image legitimately changes image-derived fields, so separating loss in translation from a change the image caused is guesswork; a false positive turns a good update into a spurious rollback. Refusing a known-unsupported shape is checkable, diffing two inspect outputs across Docker versions is not.
   - **Rejected:** retaining the prior container after success as a rollback window. It needs a third product-owned name and a rename on the success path to stay distinguishable from residue, because labels are immutable after creation and an in-flight marker cannot be flipped to retained. More decisively, rollback after a *successful* update is a capability the brief does not carry: it ties restore to a health check that did not pass.
5. **One validation contract across C# and PowerShell build types.**
   - **Chosen:** node-template's script flow calls the Config module.
   - **Rejected:** a parallel PowerShell validator. Two implementations of one contract diverge.
   - **Rejected:** porting node-template into Forge now. It rewrites a working flow to obtain one capability, which is close to the replacement the brief rules out.
   - **Rejected:** NUKE's built-in parameters file. It is JSON-only, has no schema version, handles unknown keys by NUKE's rules not ours, and does not reach node-template.
6. **Configuration precedence placement.**
   - **Chosen:** module configuration at the argument level below explicit arguments, and map-derived environment below process environment.
   - **Rejected:** module configuration passed as environment. That would silently invert its current precedence over environment variables, a behaviour change with no requirement behind it.
   - **Rejected:** leaving map-file values overwriting the process environment. A generated file would then outrank a live environment variable, contradicting the brief's "arguments over environment variables" as a user reads it.
7. **Surface baseline source.**
   - **Chosen:** the manifest attached to the previous published release.
   - **Rejected:** regenerating the baseline from the previous tag's source. That depends on old code still building with today's toolchain for years.
   - **Rejected:** a manifest committed to the tree. It can be edited to make a break pass.

## Open questions

1. **What does a push to `main` publish from 2.0.0 onward?** Main pushes currently publish both `latest` and a versioned tag. Under ContinuousDelivery on `main`, successive commits compute the same version until a release tag exists. The next push would therefore overwrite an existing versioned tag, which the immutability promise forbids, and the claim check would refuse every main push after the first. Options:
   - **(a)** Main pushes move only `latest`, and versioned tags come only from releases. **Recommended:** it keeps `latest` movable and not discouraged, and never writes a version outside a release.
   - **(b)** Main pushes publish a unique pre-release version (for example with a commit-count suffix) plus `latest`. Every main push then becomes a full release through the claim, notes and surface gates, and pre-releases accumulate on the tool feed and the Gallery.
   - **(c)** Main pushes publish nothing, and `latest` moves only on release. `latest` then means "newest release" rather than "newest main".
2. **Should the PowerShell module's default image stay `latest`, or pin to the module's own version?** The global tool pins to its own version, so that "released together" means a module or tool version runs its matching image.
   - Changing the module default is a 2.0.0 breaking change; the migration guide must carry it.
   - Keeping `latest` means a pinned module can run a newer image with a different surface.
   - **Recommended:** pin, with the image reference still overridable. The brief's non-goal protects `latest` from removal and discouragement, not its use as a launcher default.
3. **Does "direct invocation" in the 2026-09-17 decision on issue #14 mean the tool must run builds without a container?** This design reads it as "installable and invokable directly from the host shell", and makes the tool a launcher for the versioned image. If it meant in-process builds on the host, *Alternatives considered* 1 reverses and the host toolchain becomes a supported environment.
4. **Is a Windows machine running Docker Desktop with Linux containers available as a self-hosted runner?** GitHub-hosted Windows runners cannot run Linux containers, so the update tests the brief requires on each supported host cannot run there on hosted runners alone.
5. **Who owns the package identities and publishing keys on the .NET tool feed and the PowerShell Gallery?** The identities must be reserved and keys stored as CI secrets before the first 2.0.0 release can publish.
