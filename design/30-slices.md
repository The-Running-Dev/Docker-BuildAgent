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

**Issue coverage.** #1 → S2, S4, S5. #11 → S6, S13. #12 → S6, S7, S8. #14 → *Blocked*, below.

## How this document is kept

Slices move from `## Outstanding` to `## Landed` as they ship. A re-run appends to `## Outstanding`
only; `## Landed` is never rewritten. Criterion ids are permanent: they are what the tracker matches
on, so a withdrawn criterion leaves a gap and the next id continues past it. Ids are never reused
and never renumbered. Gaps in the numbering below are withdrawn criteria, listed with each slice.

## Outstanding

*None.* S1–S13 have all landed and their tracker issues are closed; what remains is under *Blocked*.

## Landed

A heading and one line per slice, from the git history and the tracker as of `93dbe28`. The full
criteria for each slice are the checklist in its tracker issue and this file at that commit
(`git show 93dbe28:design/30-slices.md`); criterion ids stay permanent, and the withdrawn ones are
named in the lines below. Where a slice landed with a stated gap, the gap is in its line.

### S1 — The public surface, derived and compared
#27, PR #49, merged 2026-09-21. The surface model, manifest derivation and comparison are in `forge/Surface`. The issue closed with S1.11 unticked: nothing outside `forge/Surface` uses `SurfaceGate`, and no workflow attaches the manifest asset or runs the gate, so no release is yet stopped by it.

### S2 — Update a running container, or refuse without touching it
#28, PRs #50 and #51, merged 2026-09-24. The Updater is in `forge/Update`. S2.14 and S2.15 could not be honored as worded (2026-09-21 entry in `90-decisions.md`). Live-daemon behaviour on Windows is the stated gap of S2.23.

### S3 — A version publishes exactly once
#29, PR #52, merged 2026-09-25. The `Release` namespace is in `forge/Release`, proved against fakes; the release workflows call `ReleasePipeline` through `forge/Publish` (2026-10-06 decision on the release entry point, which closes the 2026-09-25 gap); the `PublishToGitHub` target remains for consuming repositories' own releases. `concurrency: group: buildagent-publish` is on all three publishing workflows. S3.8 and S3.9 are withdrawn.

### S4 — When the new image does not come up, the old one comes back
#30, PR #53, merged 2026-09-25. Health wait, restore and exit statuses `10` to `12` are in `forge/Update`. S4.13 is withdrawn.

### S5 — Clearing a lock left by an update that was killed
#31, PR #56, merged 2026-09-26. `update --clear-lock` is in `forge/Update`. S5.5, S5.6 and S5.7 are withdrawn.

### S6 — One configuration file per project, validated completely
#32, PR #57, merged 2026-09-27. Discovery, parsing and validation are in `forge/Config`. The issue closed with S6.14 unticked; S13 delivered it for the four build types. The criterion that `node-template` call the validator is withdrawn (U-5).

### S7 — One predictable precedence across every source
#33, PR #58, merged 2026-09-27. The precedence merge is in `forge/Config`.

### S8 — A working example for every build type, in both formats
#34, PR #59, merged 2026-09-28. The ten samples are under `samples/`, validated by `forge/Config.Tests/SampleConfigurationTests.cs`.

### S9 — The PowerShell module runs its own image version
#35, PR #60, merged 2026-09-28.

### S10 — Documentation cannot name what the product does not have
#36, PR #61, merged 2026-09-28. The docs check is `forge/DocsCheck`.

### S11 — One page that states the promise, and a way to move to 2.0.0
#37, PR #62, merged 2026-09-28.

### S12 — Every document about a public surface has one owner
#38, PR #63, merged 2026-09-28.

### S13 — The build reads the project's configuration file before it runs
#69, PRs #71 and #73, merged 2026-09-30 and 2026-10-01. `Base.Build` resolves the project file before generating any environment file. The `node-template` flow is not covered (U-5).

## Blocked

Each item names a contract `## Unresolved` entry. None may be answered by an implementing slice.

**The global tool's published entry point — issue #14 (U-1).** Resolved by the 2026-10-06 decision
on the tool's identity: the package is `BuildAgent.Tool` on nuget.org, the command is `build-agent`,
and the module goes to the PowerShell Gallery. The tool's `ToolCommand` manifest items are no longer
blocked; no slice derives them yet.

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

**The unsupported container shape list (U-10).** Resolved by the 2026-10-06 decision on
unreproduced settings: beyond anonymous volumes and legacy container links, the update refuses
every setting the replacement does not carry over, and carries the logging configuration over.

**The wrapper's and NUKE's own parameters on the surface (U-15).** The build wrapper's
parameters and the `ChangeLogSource` field are documented but derived into no manifest item, so
no slice can protect them or clear the docs check's ten recorded findings until U-15 is decided.

**Recovering prior-container residue.** The contract's tool surface has `update --clear-lock` and no
command that returns a prior container left behind by an interrupted update. An operator can
therefore clear the lock but has no supplied way to complete the restore, and S5's criteria for it
are withdrawn. This needs a contract amendment before a slice can land it — it is a gap in the
contract rather than an open question already recorded in it.

---

Run `/track` next to open issues from these slices. This document opens none.
