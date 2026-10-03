# Pull Request

## Linked Issue or Slice
<!-- Closes #<issue>, or the slice id (for example S12.3) from design/. Write "none" for a small fix. -->

## Changes Made
<!-- Describe what this PR changes and why -->

## Type of Change
<!-- Check the appropriate box -->
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Maintenance (refactoring, dependencies, etc.)

## Contract Impact
<!-- A protected surface is the image, the build command, the global tool, the project configuration file,
Docker-template discovery or the PowerShell module. See design/20-contract.md and PSModule.requirements.md. -->
- [ ] None: no protected surface changes
- [ ] Changes a protected surface (name the surface and the contract text that changes):

## Release Impact
<!-- Check if this should be included in the next release -->
- [ ] Include in release notes
- [ ] Skip release notes (internal/maintenance changes)

## Testing
<!-- Describe how you tested these changes -->
- [ ] Tests added/updated
- [ ] `dotnet test forge/Forge.sln` passes
- [ ] DocsCheck passes: `dotnet run --project forge/DocsCheck -c Release -- .`
- [ ] Pester tests pass, if the PowerShell module changed
- [ ] Manual testing completed
