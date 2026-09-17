# Brief — Docker-BuildAgent

> Written by me, not by a model. A model may interrogate it (`/brief-check`) but not author it.

> **MODEL DRAFT — not yet owned.** Assembled on 2026-09-17 from the tree, the GitHub tracker, and the local graphify graph (`graphify-out/GRAPH_REPORT.md`: 1620 nodes, 129 communities). Statements marked **Observed** were checked against the tree or the tracker. Everything marked **Decide** is a proposal only I can confirm, reject, or rewrite. This file is not a brief until every **Decide** is resolved and this notice is deleted.

## Problem

**Observed:**

- **Every consumer takes whatever `main` last published.** Six of my repositories build with the image (Data, Docker-DNSAtHome, Docker-Watchdog, Docs-Template, Portfolio, Wiki), and all six name `:latest`, and a push to `main` here republishes that tag. A breaking change in this repository reaches all of them on their next build, with no version boundary and nothing that tests a consumer before it does.
- **What the product *is* has several competing descriptions and no single owner.** The README, `.github/copilot-instructions.md`, the published `documentation/` site, `PSModule.requirements.md`, `PSModule.specs.md`, `.github/DOCUMENTATION-UPDATES.md`, and two root `.docx` reports all describe overlapping ground. graphify groups the four PowerShell-module documents as one redundant set. They already disagree with the tree: the published architecture page describes a `forge/NodeTemplate/` project that does not exist; that build lives in `scripts/nuke/build.ps1`.
- **Nothing records what must stay true.** `design/10-design.md` through `30-slices.md` are empty. The only decisions logged are about installing AgentKit. Behaviour consumers depend on, such as the `build <type>` command surface, the `.build/` mapping-file syntax, the Docker-template discovery order, and the PowerShell module's exported members, lives only in code and in whichever document last described it.
- **The backlog has no decided scope.** Four feature issues have been open since mid-2025: #1 rollback on unhealthy update, #11 YAML/JSON configuration, #12 generic GitHubProject configuration, #14 a unified global CLI. None is accepted or rejected.

**Decide:** which of these is the problem this brief exists to solve. My guess is the first two, but that is intent, and intent is yours.

## Who it is for

**Observed:** me, sole maintainer and effectively the sole committer. My own repositories, listed above, are the known consumers. The image is public on GHCR, MIT-licensed, and has a public docs site.

**Decide:** are outside users a supported audience, meaning they get a compatibility promise, or only tolerated? That one answer sets how much of the problem above is a defect and how much is acceptable.

## Non-goals

**Decide.** These are candidates only; none binds until you keep it.

- Owning or changing consuming repositories. Their workflows are theirs. *(`AGENTS.md` already says this repository does not own them.)*
- Changing `docs-template/` content from here. It is a pinned submodule of Docusaurus-Template.
- Multi-architecture images. The image is amd64 today; QDK installs from an `amd64` `.deb`.
- Build types beyond the current five (`docker`, `node`, `node-in-docker`, `node-template`, `forge`).
- A general-purpose CI product for third parties.
- Issue #14, the unified global CLI, and issue #1, rollback on unhealthy update. Keep, defer, or reject each explicitly.

## Definition of done

**Decide.** These are proposed, checkable forms of "fixed" for the problems above.

- A consumer can pin an immutable image version, and at least one of my consumer repositories does.
- A breaking change to the `build` command surface, the `.build/` file syntax, or the PowerShell module's exported members cannot reach `:latest` without a version bump that says so.
- Each of those three surfaces has exactly one canonical document. Every other document links to it rather than restating it.
- The published docs describe no path, project, or parameter the tree lacks.
- Issues #1, #11, #12, and #14 are each either scheduled against a slice or closed as a non-goal.

## Environment

**Observed:**

- **Image:** Debian bookworm, from the `javascript-node:22` devcontainer base, which can be overridden. It carries .NET SDKs 8, 9, and 10, NUKE, GitVersion, PowerShell, the Docker CLI with buildx, Angular CLI, pnpm, and QDK. Container builds need the host Docker socket mounted.
- **Forge:** a .NET 8 NUKE solution of four build projects over a shared `Common` library, with xUnit test projects.
- **CI:** GitHub Actions on `ubuntu-latest`. `ci.yml` runs on pull requests. `build.yml` runs on pushes to `main` and publishes the image. `release.yml` and `release-tag.yml` create releases. `docs.yml` deploys to GitHub Pages.
- **PowerShell module:** Windows PowerShell 5.1 or later. It drives the container over a Docker host endpoint, by default `tcp://host.docker.internal:2375`.
- **Scale:** single user. Builds are invoked one at a time per consumer, with no shared state between runs.

**Decide:** is a Windows workstation plus GitHub Actions the whole supported environment, or do other CI hosts count?

## Lifespan

**Observed:** active since 2025-05-26, 147 commits. The image is a build dependency of at least six other repositories.

**Decide:** proposed as maintained for years. That justifies the full pipeline (contract, slices, tracker) rather than ad-hoc fixes.
