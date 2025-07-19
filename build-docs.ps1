Set-Location docs-ui

.\setup.ps1 `
    -projectDirectory $PSScriptRoot `
    -documentationDirectory $(Join-Path $PSScriptRoot "docs-ui")

& pnpm install

& pnpm run build:prod