# decision/2026-10-17-nuget-trusted-publishing
Date: 2026-10-17
Anchor: 2026-10-17 — nuget.org is published to through Trusted Publishing; the PowerShell Gallery keeps an API key
Status: accepted
StatedIn: unit/document/design-10-design § Control flow 2

## Claim
The release workflows publish `BuildAgent.Tool` with a one-hour key from nuget.org Trusted Publishing (`id-token: write`, `NuGet/login`, the `release` environment variable `NUGET_USER`, one nuget.org policy per release workflow file restricted to the `release` environment); no `NUGET_API_KEY` secret exists. `PSGALLERY_API_KEY` remains a secret in the `release` environment. `scripts/Initialize-ReleasePublishing.ps1` sets up and checks what GitHub holds.
