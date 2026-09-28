# decision/2026-09-19-stale-lock-reported-never-taken-over
Date: 2026-09-19
Anchor: 2026-09-19 — A stale update lock is reported, never taken over, and the pull moves outside it
Status: accepted
StatedIn: unit/document/design-10-design § Update lock

## Claim
The image pull runs before the lock is taken, and a lock past its deadline is reported and refuses the update; clearing it is an operator action.
