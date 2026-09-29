# decision/2026-09-17-update-lock-never-started-container
Date: 2026-09-17
Anchor: 2026-09-17 — Update lock is a never-started container at the daemon
Status: accepted
StatedIn: unit/document/design-10-design § Update lock

## Claim
The update lock is a never-started container whose unique name is the mutex, held at the daemon so it excludes every client.
