# decision/2026-10-03-claim-starts-with-atomic-ref
Date: 2026-10-03
Anchor: 2026-10-03 — The release claim starts with an atomic git ref
Status: accepted
StatedIn: unit/document/design-10-design § Concurrency and ordering

## Claim
The claim begins by creating `refs/release-claims/v<version>` at the commit through the refs API, which refuses an existing ref; a ref at another commit refuses the run, and the CI concurrency group stays the first guard.
