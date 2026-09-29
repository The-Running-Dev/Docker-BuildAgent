# decision/2026-09-20-project-configuration-file-name
Date: 2026-09-20
Anchor: 2026-09-20 — The project configuration file is `buildagent.{yml,yaml,json}` with one spelling per key
Status: accepted
StatedIn: unit/document/design-20-contract § Project configuration file

## Claim
The project file is `buildagent` with a `.yml`, `.yaml` or `.json` extension, carries an integer `schemaVersion`, and accepts each key only in its kebab-case flag spelling.
