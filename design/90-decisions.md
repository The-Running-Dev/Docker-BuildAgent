# Decision log

Append-only. Newest at the top. The rejected alternatives are the point — without them, every future session relitigates the same choice.

## Open
<A staging area, not a home. Things noticed mid-slice that were deliberately not acted on. `/track` turns each into a GitHub issue and removes it from here. An item that is a *decision* rather than a *todo* belongs below as an entry, not in an issue.>

---

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
