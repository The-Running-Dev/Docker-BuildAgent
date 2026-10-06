# decision/2026-10-06-update-refuses-unreproduced-settings
Date: 2026-10-06
Anchor: 2026-10-06 — An update refuses every setting its replacement does not carry over, and pulls before it resolves
Status: accepted
StatedIn: unit/document/design-20-contract § Updater

## Claim
An update refuses, naming them, every container setting its replacement does not carry over (image-suppliable settings only when they differ from the image's), carries the logging configuration over, always pulls before resolving the image, and handles a first interrupt by releasing or restoring.
