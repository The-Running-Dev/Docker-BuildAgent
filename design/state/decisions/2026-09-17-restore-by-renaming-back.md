# decision/2026-09-17-restore-by-renaming-back
Date: 2026-09-17
Anchor: 2026-09-17 — Container update restores by renaming the original container back
Status: accepted
StatedIn: unit/document/design-10-design § Prior image pin and prior container

## Claim
An update renames the original container aside and pins its image; restore renames it back and starts it, so nothing is recreated from inspect output.
