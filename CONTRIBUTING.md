# Contributing

Thanks for helping. This page is the short version; the
[Development Guide](https://build-agent.subzerodev.com/docs/architecture/development-guide) has the detail,
and [`.github/copilot-instructions.md`](.github/copilot-instructions.md) is the map of what is where.

By taking part you agree to the [Code of Conduct](CODE_OF_CONDUCT.md). Report security problems as
described in [SECURITY.md](SECURITY.md), not in an issue.

## Before you start

- Open an issue first for anything beyond a small fix, using the Bug or Story template, or ask in
  [Discussions](https://github.com/The-Running-Dev/Docker-BuildAgent/discussions).
- Clone with submodules: `git clone --recurse-submodules`, or `git submodule update --init` in an existing
  clone. `docs-template/` is a pinned submodule.
- You need the .NET 8 SDK, PowerShell 5.1 or later (Pester 5 for the module tests) and Git. Docker is needed
  only to build the image or run a Docker build; Node.js 22 only to preview the documentation site.

## Make the change

1. Branch from `main`.
2. Change the code with its tests.
3. Run the checks below.
4. Open a pull request using the template and wait for CI. Do not rewrite history that has been pushed; add a
   follow-up commit.

Commit messages use a lowercase conventional prefix: `fix: ...`, `docs: ...`, `feat: ...`, `ci: ...`.

## Checks to run

From the repository root:

```bash
dotnet test forge/Forge.sln
dotnet run --project forge/DocsCheck -c Release -- .
pwsh scripts/sync-site-home.ps1 -Check
pwsh scripts/Update-ParameterDocs.ps1 -Check
```

```powershell
Invoke-Pester -Path scripts/powershell-module/Docker-BuildAgent.Tests.ps1 -CI -Output Detailed
```

CI runs the same checks. `sync-site-home.ps1` regenerates `documentation/src/pages/index.md` from
`README.md`, and `Update-ParameterDocs.ps1` regenerates the tables in `documentation/docs/parameters.md`;
run either without `-Check` after editing its source.

## Public surfaces and the design chain

The image, the `build` command, the global tool, the project configuration file, Docker-template discovery
and the PowerShell module are protected surfaces. A change to a name, default or behavior on one of them is
a compatibility change: its contract is [`design/20-contract.md`](design/20-contract.md) (and
[`PSModule.requirements.md`](PSModule.requirements.md) for the module), and the pull request must say so.
A documentation page that explains a surface carries a `Canonical contract:` line naming the document that
owns it; see `design/docs-classification.txt`.

## Documentation

The pages are in `documentation/docs/`. To preview the site with hot reload:

```powershell
./scripts/build-docs-local.ps1
```

It needs `pnpm` and the `docs-template/` submodule, and serves http://localhost:3000.
