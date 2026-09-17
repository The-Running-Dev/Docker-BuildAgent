# Decision log

Append-only. Newest at the top. The rejected alternatives are the point — without them, every future session relitigates the same choice.

## Open
<A staging area, not a home. Things noticed mid-slice that were deliberately not acted on. `/track` turns each into a GitHub issue and removes it from here. An item that is a *decision* rather than a *todo* belongs below as an entry, not in an issue.>

---

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
