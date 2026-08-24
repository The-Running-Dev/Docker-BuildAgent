# Decision log

Append-only. Newest at the top. The rejected alternatives are the point — without them, every future session relitigates the same choice.

## Open
<A staging area, not a home. Things noticed mid-slice that were deliberately not acted on. `/track` turns each into a GitHub issue and removes it from here. An item that is a *decision* rather than a *todo* belongs below as an entry, not in an issue.>

---

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
