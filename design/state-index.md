# Design state — index

The corpus-wide facts a single record cannot state alone: the unit table of contents, and the
four reverse edges a unit record does not carry directly (`Invariant.BoundBy`,
`Contract.Consumers`, `Decision.Affects`, `Question.Affects`). Every table below is a
**projected** marked region (`AGENTS.shared.md` § *Marked regions*) — rendered by
`tools/Update-DesignProjection.ps1` from `design/state/`, and overwritten on every
regeneration. Nothing here is written by hand.

This repository has not yet written unit, invariant, contract, decision, or question records
under `design/state/` — only the work mirror (`design/state/work/`, refreshed by `/track`) exists
so far. The tables below are empty for that reason, not because nothing is true; they fill in as
those records are written.

## Units

<!-- units:start -->
| Id | Kind | Anchor |
|---|---|---|
| _(no active unit records yet)_ | | |
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
| _(no decision records yet)_ | |
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
| 1 | #1 | Rollback on Unhealthy Update | — | `0189dd83bf03c2979300bf4b45d1921669af0bcb` |
| 1 | #14 | Unified Build Orchestration via Global CLI Tool | — | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 4 | #27 | S1 — The public surface, derived and compared | S1.1, S1.2, S1.3, S1.4, S1.5, S1.6, S1.7, S1.8, S1.9, S1.10, S1.11, S1.12, S1.13, S1.14 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 5 | #28 | S2 — Update a running container, or refuse without touching it | S2.1, S2.2, S2.3, S2.4, S2.5, S2.6, S2.7, S2.8, S2.9, S2.10, S2.11, S2.12, S2.13, S2.14, S2.15, S2.16, S2.17, S2.18, S2.19, S2.20, S2.21, S2.22, S2.23 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 6 | #29 | S3 — A version publishes exactly once | S3.1, S3.2, S3.3, S3.4, S3.5, S3.6, S3.7, S3.10, S3.11, S3.12, S3.13, S3.14 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 7 | #30 | S4 — When the new image does not come up, the old one comes back | S4.1, S4.2, S4.3, S4.4, S4.5, S4.6, S4.7, S4.8, S4.9, S4.10, S4.11, S4.12 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 8 | #31 | S5 — Clearing a lock left by an update that was killed | S5.1, S5.2, S5.3, S5.4 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 9 | #32 | S6 — One configuration file per project, validated completely | S6.1, S6.2, S6.3, S6.4, S6.5, S6.6, S6.7, S6.8, S6.9, S6.10, S6.11, S6.12, S6.13, S6.14, S6.15, S6.16, S6.17 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 10 | #33 | S7 — One predictable precedence across every source | S7.1, S7.2, S7.3, S7.4, S7.5, S7.6, S7.7, S7.8, S7.9, S7.10 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 11 | #11 | Support YAML and JSON for Configuration | — | `0189dd83bf03c2979300bf4b45d1921669af0bcb` |
| 11 | #34 | S8 — A working example for every build type, in both formats | S8.1, S8.2, S8.3, S8.4, S8.5, S8.6, S8.7 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 12 | #12 | Add Generic Configuration for GitHubProject | — | `0189dd83bf03c2979300bf4b45d1921669af0bcb` |
| 12 | #35 | S9 — The PowerShell module runs its own image version | S9.1, S9.2, S9.3, S9.4, S9.5, S9.6, S9.7, S9.8, S9.9 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 13 | #36 | S10 — Documentation cannot name what the product does not have | S10.1, S10.2, S10.3, S10.4, S10.5, S10.6, S10.7, S10.8, S10.9 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 14 | #37 | S11 — One page that states the promise, and a way to move to 2.0.0 | S11.1, S11.2, S11.3, S11.4, S11.5, S11.6, S11.7, S11.8 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
| 15 | #38 | S12 — Every document about a public surface has one owner | S12.1, S12.2, S12.3, S12.4, S12.5, S12.6 | `9e4b4c460d4117f7af5d545ab680c8b3f693944b` |
<!-- outstanding:end -->
