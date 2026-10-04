# decision/2026-10-03-updater-owns-its-notifier
Date: 2026-10-03
Anchor: 2026-10-03 — The Updater owns its webhook notifier and depends on no Build-types code
Status: accepted
StatedIn: unit/document/design-10-design § Module boundaries

## Claim
The Updater sends its notification through its own webhook sender, sharing only the setting name with Build types and no code; the Updater → Notifications edge is dropped.
