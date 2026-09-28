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
| 11 | #11 | Support YAML and JSON for Configuration | — | `0189dd83bf03c2979300bf4b45d1921669af0bcb` |
| 12 | #12 | Add Generic Configuration for GitHubProject | — | `0189dd83bf03c2979300bf4b45d1921669af0bcb` |
<!-- outstanding:end -->
