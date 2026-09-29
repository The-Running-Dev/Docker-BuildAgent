# decision/2026-09-19-update-lock-keyed-on-name
Date: 2026-09-19
Anchor: 2026-09-19 — Update lock is keyed on the container name, not the container id
Status: accepted
StatedIn: unit/document/design-10-design § Update lock

## Claim
The lock container's name derives from the target container's name, which stays stable across the rename and re-create an update performs.
