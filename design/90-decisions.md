# Decision log

Append-only. Newest at the top. The rejected alternatives are the point — without them, every future session relitigates the same choice.

## Open
<A staging area, not a home. Things noticed mid-slice that were deliberately not acted on. `/track` turns each into a GitHub issue and removes it from here. An item that is a *decision* rather than a *todo* belongs below as an entry, not in an issue.>

---

### 2026-09-19 — The surface gate fails on any manifest difference it does not name compatible
Context: Red-team finding F3 against `design/10-design.md` @ f9532d2, adjudicated as a defect. The manifest recorded parameter types, the *ordered* list of Docker-template discovery locations, and the image's invocation inputs including its entrypoint, while step 3 failed on four conditions only — removal, rename, changed default, and removal without prior deprecation. A minor release could therefore change a parameter's type, replace the entrypoint, or reorder discovery and pass every designed gate, and the brief's *Compatibility promise* makes each of those breaking: it names changed documented behaviour alongside removal and rename, and names the entrypoint and Docker-template discovery as protected surfaces directly. The manifest's own *What it cannot record* clause did not excuse it — that clause assigns behaviour to release notes because the manifest cannot see it, and here the manifest held the data and the comparison did not read it. Investigation found a second gap the finding did not name: the item schema carried type and default "for parameters and config keys" and gave every other item nowhere to put its value, so the entrypoint's own string was unrecordable and the comparison was undefined rather than merely narrow.
Chosen: Invert the rule. Any difference from the baseline fails without a major increase, except an enumerated compatible set — adding an item, marking an existing item deprecated, adding a supported schema version — with removal still requiring prior deprecation at any major. Give every item a comparable value, and state that an item whose value cannot be recorded is not in the manifest, since a gate that fails on any difference cannot fail on what it does not hold.
Rejected: Extend the enumerated failure conditions with changed type, changed value, and changed order — it fixes the three instances and leaves the class, because each field later added to the manifest goes unchecked until someone remembers to write a rule for it and nothing reports the omission; a whitelist's failure mode is a loud false failure a maintainer answers, a blacklist's is silence. Narrow the manifest to record only what the four conditions read, pushing type, entrypoint, and order to the release-notes duty — self-consistent, but it abandons mechanical enforcement on surfaces the brief protects by name, against a definition of done that asks for a mechanical comparison wherever one is possible. Keep the gate and accept the risk — an immutable public minor release cannot be withdrawn, so the first occurrence permanently spends a version whose number asserts a compatibility it does not have.
Reversibility: cheap — prose only; `20-contract.md` and `30-slices.md` are still empty. The accepted cost runs the other way now: the gate will fail on differences that are in fact compatible, each answered by naming it in the compatible set or by raising the major. That is a maintainer cost paid per occurrence, against a consumer cost that cannot be paid at all. Noted and not acted on: the definition of done names only removal and rename as mechanical failures. This reads that as a floor rather than as the definition of breaking, which *Compatibility promise* holds and states more broadly; tightening the definition-of-done wording would be a brief amendment and belongs to `/brief-check`.

### 2026-09-19 — An update refuses a container whose replacement cannot be created faithfully
Context: Red-team finding F7 against `design/10-design.md` @ f9532d2, adjudicated as a defect. The design rejected inspect-based re-creation for restore because it is lossy — anonymous volumes, links, some network and runtime options — and then created the replacement from exactly that source at step 8. The asymmetry would be harmless if the lossy path were always recoverable, and on a failed update it is, since restore renames the original back. The damage is on the success path: step 9 gates only on the new container's own health, and a container that lost an anonymous volume can be entirely healthy, after which step 10 removes the prior container. The one gate that could see the loss cannot, and the one artifact that could undo it is destroyed. Step 2's existing refusal did not cover this — its criterion is that the prior configuration cannot be *restored* exactly, and both its cases are about restore being impossible, which is a different operation from creating faithfully.
Chosen: Add a second, distinct refusal at step 2 for a target whose replacement cannot be created faithfully from its inspected configuration, kept separate from the restore criterion because the two fail for different reasons. State the invariant it exists to hold — the replacement differs from the target in image only — and note at step 8 and in the failure modes that health is not evidence of configuration fidelity, which is why this is a refusal before any change rather than a check afterwards. The enumerated set of supported and refused configuration shapes is a contract item; this document states the rule, not the list.
Rejected: Compare the new container's inspect output against the original's before removing the prior container — the comparison set is large and moves with every Docker version, a new image legitimately changes image-derived fields, and a false positive turns a good update into a spurious rollback. Retain the prior container after success as a rollback window — it needs a third product-owned name and a success-path rename to stay distinguishable from residue, since labels are immutable and an in-flight marker cannot be flipped, and rollback after a successful update is a capability the brief does not carry.
Reversibility: cheap — prose only; `20-contract.md` and `30-slices.md` are still empty. The accepted cost is that some existing containers become un-updatable by this tool, which is the honest outcome: refusing loudly beats degrading silently, and refuse-before-changing is the posture the brief already takes. Noted and not acted on: the brief promises the update preserves the prior *image* and is silent on configuration fidelity. This treats fidelity as the plain meaning of updating a named container; stating it in the brief as well would be a brief amendment and belongs to `/brief-check`.

### 2026-09-19 — A stale update lock is reported, never taken over, and the pull moves outside it
Context: Red-team finding F2 against `design/10-design.md` @ f9532d2, adjudicated as a defect. Takeover removed the lock by id and re-created it, which excluded other takers but not the owner — no removal stops a running process, and the owner never re-read its lock. The deadline was acquisition plus health timeout plus a margin, but the section it covered began at lock acquisition and contained the registry pull, so a first pull of a large image over a slow link routinely looked like a dead owner. The document's own claim that skew makes takeover "early or late, never double" was unsound for the same reason: early takeover is double, because nothing stops the owner. Investigation also showed *Failure modes* → *Process interruption* already recovered crashes through the residue refusal rather than through takeover, so takeover's only real coverage was a crash during the pull.
Chosen: Move the pull and the cheap restorability refusal ahead of lock acquisition, so the one unbounded phase is outside the critical section and a pull failure strands no lock. Then delete automatic takeover: a lock past its deadline is reported — owner, process, age, how far past — and the update refuses. The deadline becomes diagnostic evidence for an operator rather than authorization for a process, which removes clock skew from correctness entirely. Clearing a dead lock is an operator action and a contract item, the posture already taken for completing a restore from residue. Residue is correspondingly redefined as a labelled prior container whose lock is absent **or past its deadline**.
Rejected: Bound the section and have the owner re-read its lock before each mutating step — Docker offers no fencing token, so the check and the following container operation are not one act and a timing-dependent window remains; it trades a provable property for one no test can demonstrate. Keep takeover as written and accept the risk — the brief states the single-update rule as a refusal guarantee, not best effort. Heartbeat the lock to extend its deadline — impossible with the chosen mechanism, since labels are immutable after creation and re-creating the lock reopens the window it exists to close.
Reversibility: cheap — prose only; `20-contract.md` and `30-slices.md` are still empty. The residual cost is stated and accepted: a crash between acquiring the lock and the first rename, a tag operation and a local log write, strands a lock an operator must clear. That window was previously a multi-minute network pull.

### 2026-09-19 — Update lock is keyed on the container name, not the container id
Context: Red-team finding F1 against `design/10-design.md` @ f9532d2, adjudicated as a defect. The lock name was derived from the target's container id, but step 8 of the update renames the original away and creates the replacement under the same name — so the id behind that name changes inside the critical section. A second update resolving the same name during the health wait derived a different lock and acquired it, defeating the brief's one-update-per-container invariant. The step 3 residue refusal could not be relied on to catch it: residue was defined against "an in-progress update" with no stated way to establish in-progress, and the update log is per-user and per-machine, so another client of the same daemon cannot read it and a start without an outcome reads the same whether the owner died or is still running.
Chosen: Derive the lock container's name deterministically from the **target container's name**, hashed to a valid container name. The name is the identity the operator supplies and the one the brief's invariant is written over, and it is stable across the rename and re-create. Redefine residue as a labelled prior container with no live lock for the name it was renamed from, which the name-keyed lock now makes a sound test.
Rejected: Keep id-derivation and resolve step 1 through the prior-container label back to the original id — more machinery, and it still leaves the window between resolving the name and taking the lock. Accept the race as rare — the brief states the single-update rule as a refusal guarantee, not a best effort. The earlier entry of 2026-09-17 records the id-derived form; this supersedes its identity clause only, and its location and takeover reasoning stand.
Reversibility: cheap — prose only; `20-contract.md` and `30-slices.md` are still empty, so nothing depends on lock identity yet. Expensive once implemented, since lock identity couples to update records, residue detection, restore, cleanup, and the concurrency tests.

### 2026-09-17 — Surface baseline is the manifest attached to the previous published release
Context: A release must fail on a removal or rename without a major version, which needs a trustworthy record of what the previous release shipped.
Chosen: Derive a surface manifest from declarations at release, attach it to the release, and compare against the highest published release below the candidate; removal additionally requires the item to be deprecated in that baseline.
Rejected: Regenerate the baseline from the previous tag's source — depends on years-old code building with today's toolchain. A manifest committed to the tree — editable to make a break pass.
Reversibility: cheap

### 2026-09-17 — Configuration precedence places module config and map files explicitly
Context: The brief fixes arguments over environment over project file over defaults, and requires mapping files and PowerShell configuration to be placed.
Chosen: Invocation arguments; then module configuration; then process environment including `set-environment.ps1`; then map-derived environment, which no longer overwrites a set variable from 2.0.0; then the project file; then defaults. The app env map is an output, not a source. Secret parameters are rejected in the project file.
Rejected: Module configuration passed as environment — silently inverts its current precedence over environment variables. Map-derived values overwriting process environment, as today — a generated file would outrank a live variable. Accepting secrets in the project file — committed files reach build contexts and images.
Reversibility: expensive

### 2026-09-17 — One configuration validator shared by every build type
Context: One validation contract must cover four Forge build types and the script-only node-template flow.
Chosen: A single Config module owns schema, validation and precedence; node-template calls it.
Rejected: A parallel PowerShell validator — two implementations of one contract diverge. Porting node-template into Forge now — rewrites a working flow for one capability. NUKE's built-in parameters file — JSON-only, no schema version, NUKE's unknown-key rules, does not reach node-template.
Reversibility: cheap

### 2026-09-17 — Container update restores by renaming the original container back
Context: An update must preserve the prior image and restore it when the health check does not pass, and restore is the path that must work.
Chosen: Stop and rename the original container, pin its image with a per-container product-owned tag, create the updated container under the original name, and on failure rename the original back and start it. Refuse before changing anything when the container auto-removes or is orchestrator-managed. Keep exactly one pin per container.
Rejected: Re-create the prior container from inspect output — lossy for anonymous volumes, links and some runtime options, so restore would be approximate. A shared `:previous` tag as in issue #1 — collides across containers of one repository and keeps no container configuration.
Reversibility: expensive

### 2026-09-17 — Update lock is a never-started container at the daemon
Context: Only one update of a given container may run at a time, and the refusal must hold for every client of the daemon and survive a crash.
Chosen: Create a never-started container with a name derived from the target's id; name uniqueness is the mutex. A lock past its deadline label is taken over by removing it by id, then re-creating by name. The update log is an append-only per-user file on the machine running the command, flushed before the first change; a start entry without an outcome or a leftover prior container is residue that makes the next update refuse.
Rejected: A client-side lock file — does not exclude a second machine using the same daemon. A named volume — creation with an existing name succeeds, so not a mutex. A label on the target — labels are immutable after creation.
Reversibility: cheap

### 2026-09-17 — Release existence is a draft-release claim bound to a commit
Context: Publishing an existing version must fail through every trigger, including the manual input, without depending on the registry, and a partial publish must be recoverable given immutable tags.
Chosen: Before any write, refuse when a published release exists, a draft exists for another commit, or any sink holds the version without a matching claim. Then create a draft release for the commit, publish image, tool and module, move `latest` last, tag, and publish the release. A re-run for the same commit resumes. One version is computed per release and stamped into all three artifacts. All publishing workflows share one queued CI concurrency group.
Rejected: Git tag existence alone — the tag-triggered workflow starts from an existing tag, and a tag says nothing about which sinks hold the version. Relying on sink duplicate rejection — the brief forbids depending on the registry, and the image registry overwrites tags. Writing the final release first — a partial publish becomes indistinguishable from a complete one.
Reversibility: expensive

### 2026-09-17 — Global tool launches the versioned image
Context: The global tool runs on Linux and Windows hosts and must expose all five build types; the brief leaves how it reaches them unspecified.
Chosen: The tool runs the image whose version matches its own, as the PowerShell module does, and also hosts the container update command.
Rejected: In-process Forge on the host — every build type would need the image's toolchain on both host platforms, and the container template fallback and script-only node-template flow have no host equivalent. The module invoking the tool — adds a .NET tool install requirement the module does not declare.
Reversibility: expensive

### 2026-09-17 — Maintain Docker-BuildAgent as a supported public product
Context: The image and documentation are public, multiple repositories consume the image, and the design retrofit must state who receives a compatibility promise and for how long.
Chosen: Support public and first-party consumers equally, maintain the product for years, support the build surface on Docker-capable hosts, and retain the PowerShell module's declared platform requirement.
Rejected: First-party-only support — it contradicts the public product commitment. Experimental or best-effort maintenance — it cannot justify durable compatibility and migration rules.
Reversibility: expensive

### 2026-09-17 — Protect public surfaces with semantic versions and immutable tags
Context: Publishing already produces `latest` and versioned image tags, while consumers and examples predominantly select `latest` and no binding compatibility rule says what a version means.
Chosen: Keep `latest` movable, make published version tags immutable, and require a major version for a breaking public-surface change.
Rejected: Prohibit all breaking changes forever — it prevents intentional evolution. Permit breaking changes in any version with release notes — pinning would reproduce an artifact without communicating compatibility.
Reversibility: expensive

### 2026-09-17 — Retain and define all four unresolved feature issues
Context: Issues #1, #11, #12, and #14 had no agreed place in the product design; #11 and #12 also lacked bodies, and #1 described a watchdog responsibility absent from the repository identity.
Chosen: Make scoped update health/rollback, YAML and JSON project configuration, a generic sample configuration, and a packaged global build tool part of this product's design. The sample configuration remains a template rather than a separate subsystem.
Rejected: Close rollback as belonging only to Docker-Watchdog — Docker-BuildAgent will own the accepted scoped capability. Close the global tool as superseded by `build <type>` — the existing wrapper does not satisfy the packaged direct-invocation goal. Leave #11 and #12 as historical documentation-template work — the accepted scope repurposes them as product configuration and its sample.
Reversibility: expensive

### 2026-08-21 — Install the two AgentKit session hooks
Context: `tools/Measure-Session.ps1` is installed, PowerShell 7 is available, and both supported hook events were absent.
Chosen: Track `.claude/settings.json` with the `SessionEnd -Hook` and `UserPromptSubmit -Watch` hooks after explicit installation approval.
Rejected: Omit the hooks — the measurement and prompt-size safeguards would remain inactive despite their script being installed.
Reversibility: cheap

### 2026-08-21 — Retain the existing broad `.claude/` ignore
Context: The repository already ignores `.claude/`, while the approved AgentKit cores and settings must be tracked.
Chosen: Keep the target's ignore rule and force-add only the named installed artifacts.
Rejected: Remove or rewrite the ignore rule — that would alter target configuration outside the installer's artifact set and widen which future local state Git reports.
Reversibility: cheap
