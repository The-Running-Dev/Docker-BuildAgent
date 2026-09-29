# decision/2026-09-29-build-path-owes-config-call
Date: 2026-09-29
Anchor: 2026-09-29 — The build path owes the call into Config; the contract stands and issue #11 stays open until it lands
Status: accepted
StatedIn: unit/document/design-30-slices § S13 — The build reads the project's configuration file before it runs

## Claim
`forge/Config` ships unreferenced by any product project, so I18 and exit status `2` are unheld on `build <type>`; the code changes to match the design through a new `/plan` slice, and issue #11 maps to that slice.
