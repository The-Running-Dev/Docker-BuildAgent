# decision/2026-09-17-configuration-precedence
Date: 2026-09-17
Anchor: 2026-09-17 — Configuration precedence places module config and map files explicitly
Status: accepted
StatedIn: unit/document/design-10-design § Configuration precedence

## Claim
Precedence runs invocation arguments, module configuration, process environment, map-derived environment, project file, defaults; secrets are rejected in the project file.
