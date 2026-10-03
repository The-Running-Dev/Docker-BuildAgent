Update the documentation of this repository to match the code and the contract. Work from the tree, not from
memory, and keep the change to documentation unless the task says otherwise.

1. WHERE THINGS LIVE
- Pages: `documentation/docs/`. The site home page `documentation/src/pages/index.md` is generated from
  `README.md`; never edit it by hand. Run `pwsh scripts/sync-site-home.ps1` after editing the README.
- The tables in `documentation/docs/parameters.md` are generated from the `*Params` classes. Run
  `pwsh scripts/Update-ParameterDocs.ps1` after a parameter changes; never edit the tables by hand.
- The contract is `design/20-contract.md` (and `PSModule.requirements.md` for the PowerShell module). A page
  explains a surface; it does not redefine it. Where a page and the contract disagree, the page is wrong.
- Do not edit the invariants, non-goals, acceptance criteria or public interfaces in `design/`. Report a
  contract problem instead of fixing it in the docs.

2. RULES FOR A PAGE
- A page that covers a protected surface carries a `Canonical contract:` line naming the document that owns it,
  for example `Canonical contract (build command): [design/20-contract.md](...)`. The line names the surfaces
  the page covers.
- `design/docs-classification.txt` classifies every document in scope. A new page needs a line there.
- Name only things that exist: paths, commands, parameters, build types and discovery locations are checked
  against the tree. Do not describe behavior the code does not have; say "not yet" where a surface is planned.
- No emoji. Link to the one page that owns a fact instead of repeating it.

3. BEFORE YOU FINISH
Run, from the repository root:
- `dotnet run --project forge/DocsCheck -c Release -- .` (names and canonical-contract rules)
- `pwsh scripts/sync-site-home.ps1 -Check` (home page matches the README)
- `pwsh scripts/Update-ParameterDocs.ps1 -Check` (parameter tables match the code)
A finding that is correct but cannot be fixed now is recorded in `design/docs-check-recorded.txt` with its
reason; do not silence the check any other way.

4. DELIVERY
- Branch from `main`, commit with a `docs:` prefix, and open a pull request from the template.
- Say in the pull request which pages changed and which checks you ran. Flag anything that needs a decision
  from the maintainer.
