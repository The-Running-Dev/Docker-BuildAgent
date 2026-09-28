# decision/2026-09-20-unreadable-log-line-refuses-updates
Date: 2026-09-20
Anchor: 2026-09-20 — An unreadable update-log line refuses every update on that host
Status: accepted
StatedIn: unit/document/design-20-contract § Update log

## Claim
An update-log line that cannot be parsed is an open record for an unknown container and refuses every update on that host until an operator clears it.
