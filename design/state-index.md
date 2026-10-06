# Design state — index

The corpus-wide facts a single record cannot state alone: the unit table of contents, and the
four reverse edges a unit record does not carry directly (`Invariant.BoundBy`,
`Contract.Consumers`, `Decision.Affects`, `Question.Affects`). Every table below is a
**projected** marked region (`AGENTS.shared.md` § *Marked regions*) — rendered by
`tools/Update-DesignProjection.ps1` from `design/state/`, and overwritten on every
regeneration. No table here is written by hand.

This repository has written document unit records, invariant records and decision records under
`design/state/`, alongside the work mirror (`design/state/work/`, refreshed by `/track`). It has
written no question records, so the questions table is empty for that reason alone; the open
questions live in `design/20-contract.md` under `## Unresolved`.

## Units

<!-- units:start -->
| Id | Kind | Anchor |
|---|---|---|
| `unit/document/agent-md` | document | `agent.md` |
| `unit/document/code-of-conduct-md` | document | `CODE_OF_CONDUCT.md` |
| `unit/document/codex-profiles` | document | `codex/PROFILES.md` |
| `unit/document/contributing-md` | document | `CONTRIBUTING.md` |
| `unit/document/design-00-brief` | document | `design/00-brief.md` |
| `unit/document/design-10-design` | document | `design/10-design.md` |
| `unit/document/design-20-contract` | document | `design/20-contract.md` |
| `unit/document/design-30-slices` | document | `design/30-slices.md` |
| `unit/document/design-90-decisions` | document | `design/90-decisions.md` |
| `unit/document/design-state-index` | document | `design/state-index.md` |
| `unit/document/issue-template-bug` | document | `.github/ISSUE_TEMPLATE/bug.md` |
| `unit/document/issue-template-story` | document | `.github/ISSUE_TEMPLATE/story.md` |
| `unit/document/project-agents-md` | document | `AGENTS.md` |
| `unit/document/psmodule-requirements-md` | document | `PSModule.requirements.md` |
| `unit/document/readme-md` | document | `README.md` |
| `unit/document/security-md` | document | `SECURITY.md` |
<!-- units:end -->

## Invariants — bound by

<!-- bound-by:start -->
| Invariant | Bound by |
|---|---|
| I1 | — |
| I2 | — |
| I3 | — |
| I4 | — |
| I5 | — |
| I6 | — |
| I7 | — |
| I8 | — |
| I9 | — |
| I10 | — |
| I11 | — |
| I12 | — |
| I13 | — |
| I14 | — |
| I15 | — |
| I16 | — |
| I17 | — |
| I18 | — |
| I19 | — |
| I20 | — |
| I21 | — |
| I22 | — |
| I23 | — |
| I24 | — |
| I25 | — |
| I26 | — |
| I27 | — |
| I28 | — |
| I29 | — |
| I30 | — |
| I31 | — |
| I32 | — |
| I33 | — |
| I34 | — |
| I35 | — |
| I36 | — |
| I37 | — |
| I38 | — |
| I39 | — |
| I40 | — |
| I41 | — |
| I42 | — |
| I43 | — |
| I44 | — |
| I45 | — |
| I46 | — |
| I47 | — |
<!-- bound-by:end -->

## Contracts — consumers

<!-- consumers:start -->
| Contract | Consumers |
|---|---|
| _(no contract records yet)_ | |
<!-- consumers:end -->

## Decisions — in force for

<!-- decision-affects:start -->
| Decision | In force for |
|---|---|
| decision/2026-08-21-install-session-hooks | `unit/document/project-agents-md` |
| decision/2026-08-21-retain-claude-ignore | `unit/document/project-agents-md` |
| decision/2026-09-17-configuration-precedence | `unit/document/design-10-design` |
| decision/2026-09-17-global-tool-launches-versioned-image | `unit/document/design-20-contract` |
| decision/2026-09-17-one-configuration-validator | `unit/document/design-10-design` |
| decision/2026-09-17-release-existence-draft-claim | `unit/document/design-10-design` |
| decision/2026-09-17-restore-by-renaming-back | `unit/document/design-10-design` |
| decision/2026-09-17-retain-four-feature-issues | `unit/document/design-10-design` |
| decision/2026-09-17-semver-and-immutable-tags | `unit/document/design-00-brief` |
| decision/2026-09-17-supported-public-product | `unit/document/design-00-brief` |
| decision/2026-09-17-surface-baseline-previous-release | `unit/document/design-10-design` |
| decision/2026-09-17-update-lock-never-started-container | `unit/document/design-10-design` |
| decision/2026-09-19-agents-md-collapsed-to-pointer | `unit/document/project-agents-md` |
| decision/2026-09-19-refuse-unfaithful-replacement | `unit/document/design-10-design` |
| decision/2026-09-19-stale-lock-reported-never-taken-over | `unit/document/design-10-design` |
| decision/2026-09-19-surface-gate-fails-on-any-difference | `unit/document/design-20-contract` |
| decision/2026-09-19-update-lock-keyed-on-name | `unit/document/design-10-design` |
| decision/2026-09-20-compatibility-evidence-stays-readable | `unit/document/design-20-contract` |
| decision/2026-09-20-direct-invocation-means-packaged-tool | `unit/document/design-10-design` |
| decision/2026-09-20-docker-objects-named-by-hash | `unit/document/design-20-contract` |
| decision/2026-09-20-main-push-moves-latest-only | `unit/document/design-10-design` |
| decision/2026-09-20-module-image-pins-to-module-version | `unit/document/design-20-contract` |
| decision/2026-09-20-project-configuration-file-name | `unit/document/design-20-contract` |
| decision/2026-09-20-unreadable-log-line-refuses-updates | `unit/document/design-20-contract` |
| decision/2026-09-20-update-verification-linux-only | `unit/document/design-10-design` |
| decision/2026-09-21-delete-stale-kit-command-copies | `unit/document/project-agents-md` |
| decision/2026-09-21-prior-objects-identified-by-name | `unit/document/design-30-slices` |
| decision/2026-09-21-sync-adds-deferral-sweep-lesson | `unit/document/agent-md` |
| decision/2026-09-25-releasepipeline-ships-standalone | `unit/document/design-30-slices` |
| decision/2026-09-27-config-takes-yamldotnet | `unit/document/design-10-design` |
| decision/2026-09-28-record-documents-and-decisions | `unit/document/design-90-decisions` |
| decision/2026-09-28-retire-per-repo-kit-tool-copies | `unit/document/design-20-contract` |
| decision/2026-09-29-build-path-owes-config-call | `unit/document/design-30-slices` |
| decision/2026-10-03-claim-records-artifact-identity | `unit/document/design-10-design` |
| decision/2026-10-03-claim-starts-with-atomic-ref | `unit/document/design-10-design` |
| decision/2026-10-03-latest-moves-last | `unit/document/design-10-design` |
| decision/2026-10-03-updater-owns-its-notifier | `unit/document/design-10-design` |
| decision/2026-10-04-completion-rerun-moves-latest | `unit/document/design-10-design` |
| decision/2026-10-04-sink-identity-recorded-before-write | `unit/document/design-10-design` |
| decision/2026-10-06-tool-identity | `unit/document/design-20-contract` |
| decision/2026-10-06-update-refuses-unreproduced-settings | `unit/document/design-20-contract` |
<!-- decision-affects:end -->

## Questions — blocks and answered

<!-- question-affects:start -->
| Question | Blocks | Answered |
|---|---|---|
| _(no question records yet)_ | | |
<!-- question-affects:end -->

## Outstanding

From a checkout with no network: the outstanding work, its order, and each item's criteria,
mirrored from GitHub by `tools/Update-WorkMirror.ps1` into `WorkRef` records
(`design/state/work/`). **GitHub stays the authority** — this table is a mirror, stale by
default, and never cited as the reason work is or is not done. `Mirrored at` names the commit
the mirror was taken at; check it against `git log` before trusting an entry that looks old.

<!-- outstanding:start -->
| Rank | Issue | Title | Criteria | Mirrored at |
|---|---|---|---|---|
| 1 | #14 | Unified Build Orchestration via Global CLI Tool | — | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 12 | #12 | Add Generic Configuration for GitHubProject | — | `0189dd83bf03c2979300bf4b45d1921669af0bcb` |
| 17 | #88 | Should the three fallback template directories become TemplateLocation surface items? | O1.1 | `dcb0e38bb6973b17d813093642800e14fbef15d0` |
<!-- outstanding:end -->
