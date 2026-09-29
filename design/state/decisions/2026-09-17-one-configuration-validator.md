# decision/2026-09-17-one-configuration-validator
Date: 2026-09-17
Anchor: 2026-09-17 — One configuration validator shared by every build type
Status: accepted
StatedIn: unit/document/design-10-design § Module boundaries

## Claim
One Config module owns schema, validation and precedence for every build type, and node-template calls it.
