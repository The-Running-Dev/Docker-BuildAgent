# decision/2026-09-20-compatibility-evidence-stays-readable
Date: 2026-09-20
Anchor: 2026-09-20 — Compatibility evidence stays readable, and an item never leaves the manifest quietly
Status: accepted
StatedIn: unit/document/design-20-contract § Invariants

## Claim
The manifest reader accepts every published `manifestSchemaVersion`, and an item present in a published manifest and absent from the candidate is a removal whatever the reason.
