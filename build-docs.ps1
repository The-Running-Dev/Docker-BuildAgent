$appDir = 'documentation'

& docker run `
    -v ./:/workspace `
    -it ghcr.io/the-running-dev/build-agent:latest `
    node-template-build `
        -appDir $appDir `
        -packageManager 'pnpm' `
        -skipInstall `
        -isProduction:$false

$appDirPath = Join-Path . $appDir -Resolve
$command = @"
Set-Location '$appDirPath'

& pnpm install

& pnpm start
"@

Start-Process pwsh `
    -ArgumentList  `
    "-NoExit", `
    "-Command", $command