# decision/2026-10-06-tool-identity
Date: 2026-10-06
Anchor: 2026-10-06 — The global tool is `BuildAgent.Tool` on nuget.org, invoked as `build-agent`, and the release workflow alone holds the publishing keys
Status: accepted
StatedIn: unit/document/design-20-contract § Global tool

## Claim
The global tool is `BuildAgent.Tool` on nuget.org with command `build-agent`; the module goes to the PowerShell Gallery as `Docker-BuildAgent`; the keys `NUGET_API_KEY` and `PSGALLERY_API_KEY` are repository secrets read only by the release workflow; packing without a release version fails.
