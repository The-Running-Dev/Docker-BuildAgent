# decision/2026-09-25-releasepipeline-ships-standalone
Date: 2026-09-25
Anchor: 2026-09-25 — S3's `ReleasePipeline` ships as a standalone, fully-tested library; the live `nuke` release target still calls the old create-or-update path
Status: accepted

## Claim
The `Release` namespace is proved by tests against fakes, and wiring `ReleasePipeline` into the NUKE `PublishToGitHub` target is an open follow-up; only the versioned-tag guard in `Docker.Configure()` changed in the live build.
