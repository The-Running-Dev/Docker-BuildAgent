# decision/2026-10-04-sink-identity-recorded-before-write
Date: 2026-10-04
Anchor: 2026-10-04 — A sink's identity is recorded just before it is written, and a package is identified by its content without the repository signature
Status: accepted
StatedIn: unit/document/design-10-design § Release claim

## Claim
The claim records a sink's identity immediately before each write to it and never replaces the identity of a sink that holds the version; a package's identity is the SHA-256 over its entries excluding `.signature.p7s`.
