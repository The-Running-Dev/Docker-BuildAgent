# decision/2026-09-27-config-takes-yamldotnet
Date: 2026-09-27
Anchor: 2026-09-27 — `Config` takes `YamlDotNet` as a product dependency, promoted from test-only
Status: accepted

## Claim
`forge/Config` parses YAML with `YamlDotNet` 16.3.0 through its representation model, and JSON with `System.Text.Json`, into one internal document shape validated by one path.
