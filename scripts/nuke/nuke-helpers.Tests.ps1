#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

# Tests for the local build scripts. Run on Windows PowerShell 5.1 and PowerShell 7.
# `dotnet` is never invoked: $env:DOTNET_EXE points at a fake script that records its arguments.

BeforeAll {
    $script:HelpersPath = Join-Path $PSScriptRoot 'nuke-helpers.psm1'
    $script:BuildScriptPath = Join-Path $PSScriptRoot 'build.ps1'
    $script:Workspace = Join-Path ([System.IO.Path]::GetTempPath()) ("nuke-tests-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $script:Workspace | Out-Null

    $script:Solution = Join-Path $script:Workspace 'Fake.sln'
    Set-Content -LiteralPath $script:Solution -Value ''

    $script:ArgsFile = Join-Path $script:Workspace 'dotnet-args.txt'

    # Invoke-SafeCommand exits the process on failure, so each build runs in a child of the same host.
    function script:Invoke-FakeBuild {
        param(
            [string]$Output,
            [int]$ExitCode,
            [switch]$IsProduction
        )

        $fake = Join-Path $script:Workspace 'fake-dotnet.ps1'
        Set-Content -LiteralPath $fake -Value @"
`$args -join ' ' | Set-Content -LiteralPath '$($script:ArgsFile)'
Write-Output '$Output'
exit $ExitCode
"@

        $command = @"
`$env:DOTNET_EXE = '$fake'
Import-Module '$($script:HelpersPath)' -Force
Invoke-DotNetBuild -ProjectOrSolution '$($script:Solution)' -OutputDirectory '$($script:Workspace)' -IsProduction:`$$($IsProduction.IsPresent)
"@

        $hostPath = (Get-Process -Id $PID).Path
        $lines = & $hostPath -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command $command 2>&1

        [pscustomobject]@{
            Text     = ($lines | Out-String)
            ExitCode = $LASTEXITCODE
        }
    }
}

AfterAll {
    Remove-Item -LiteralPath $script:Workspace -Recurse -Force -ErrorAction SilentlyContinue
}

Describe 'Invoke-DotNetBuild' {
    It 'prints the compiler errors when a <Mode> build fails' -TestCases @(
        @{ Mode = 'development'; IsProduction = $false }
        @{ Mode = 'production'; IsProduction = $true }
    ) {
        param($Mode, $IsProduction)

        $result = Invoke-FakeBuild -Output 'Program.cs(3,1): error CS1002: ; expected' -ExitCode 3 -IsProduction:$IsProduction

        $result.ExitCode | Should -Be 3
        $result.Text | Should -Match 'error CS1002'
    }

    It 'stays quiet when the build succeeds' {
        $result = Invoke-FakeBuild -Output 'compiler chatter' -ExitCode 0

        $result.ExitCode | Should -Be 0
        $result.Text | Should -Not -Match 'compiler chatter'
    }

    It 'restores packages in a development build, so a fresh clone builds' {
        Invoke-FakeBuild -Output '' -ExitCode 0 | Out-Null

        Get-Content -LiteralPath $script:ArgsFile -Raw | Should -Not -Match '--no-restore'
    }
}

Describe 'build.ps1' {
    # $PSBoundParameters inside a function holds that function's parameters, so a function that reads it
    # never sees the script's switches: -isProduction:$false was silently turned back on.
    It 'reads $PSBoundParameters only at script scope' {
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($script:BuildScriptPath, [ref]$null, [ref]$null)

        $insideFunctions = $ast.FindAll({
                param($node)
                $node -is [System.Management.Automation.Language.VariableExpressionAst] -and
                $node.VariablePath.UserPath -eq 'PSBoundParameters' -and
                $null -ne $node.Parent -and
                $(
                    $parent = $node.Parent
                    while ($parent -and $parent -isnot [System.Management.Automation.Language.FunctionDefinitionAst]) {
                        $parent = $parent.Parent
                    }
                    $null -ne $parent
                )
            }, $true)

        $insideFunctions | Should -BeNullOrEmpty
    }
}
