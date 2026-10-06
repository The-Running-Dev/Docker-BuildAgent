#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

# Module tests. Run on Windows PowerShell 5.1 and PowerShell 7; both are release gates.
# `docker` is never invoked: every daemon call goes through Invoke-Docker, which is mocked.

BeforeAll {
    $script:ModuleName = 'Docker-BuildAgent'
    $script:ManifestPath = Join-Path $PSScriptRoot 'Docker-BuildAgent.psd1'
    $script:ModuleVersion = (Test-ModuleManifest -Path $script:ManifestPath).Version.ToString()
    $script:Workspace = Join-Path ([System.IO.Path]::GetTempPath()) ("ba-tests-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $script:Workspace | Out-Null

    function script:Import-FreshModule {
        Remove-Module $script:ModuleName -Force -ErrorAction SilentlyContinue
        Import-Module $script:ManifestPath -Force
    }

    function script:Get-Failure {
        param([scriptblock]$Action)
        try { & $Action; return $null } catch { return $_ }
    }
}

AfterAll {
    Remove-Module $script:ModuleName -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $script:Workspace -Recurse -Force -ErrorAction SilentlyContinue
}

Describe 'Default image (S9.1)' {
    BeforeEach { Import-FreshModule }

    It 'is the module''s own version, not latest' {
        $BuildAgentConfig.DockerImage | Should -Be "ghcr.io/the-running-dev/build-agent:$script:ModuleVersion"
        $BuildAgentConfig.DockerImage | Should -Not -BeLike '*:latest'
    }

    It 'is the module''s own pre-release when the module is one' {
        # The release stamps a pre-release module with PSData Prerelease; its image carries the same label.
        $copy = Join-Path $script:Workspace 'prerelease-module'
        New-Item -ItemType Directory -Path $copy -Force | Out-Null
        Copy-Item -Path (Join-Path $PSScriptRoot 'Docker-BuildAgent.psm1') -Destination $copy
        $manifest = Get-Content -LiteralPath $script:ManifestPath -Raw
        $manifest = $manifest -replace '(?m)^(\s*)PSData = @\{', "`$0`r`n`$1    Prerelease = 'rc1'"
        Set-Content -LiteralPath (Join-Path $copy 'Docker-BuildAgent.psd1') -Value $manifest

        Remove-Module $script:ModuleName -Force -ErrorAction SilentlyContinue
        Import-Module (Join-Path $copy 'Docker-BuildAgent.psd1') -Force

        $BuildAgentConfig.DockerImage | Should -Be "ghcr.io/the-running-dev/build-agent:$script:ModuleVersion-rc1"
    }
}

Describe 'Set-BuildAgentConfig defaults (R-CONFIG-002)' {
    BeforeEach {
        Import-FreshModule
        Mock Write-Host -ModuleName $script:ModuleName {}
    }

    It 'leaves ArtifactsDir at artifacts when -ArtifactsDir is not passed' {
        Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace

        $BuildAgentConfig.ArtifactsDir | Should -BeExactly 'artifacts'
    }
}

Describe 'Invoke-Build' {
    BeforeEach {
        Import-FreshModule
        Mock Invoke-Docker -ModuleName $script:ModuleName { [pscustomobject]@{ ExitCode = 0; Output = @() } }
        Mock Write-Host -ModuleName $script:ModuleName {}
    }

    Context 'Image override (S9.2)' {
        It 'uses an overridden reference verbatim' {
            Set-BuildAgentConfig -DockerImage 'example.com/team/agent:latest' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            Invoke-Build -type docker

            Should -Invoke Invoke-Docker -ModuleName $script:ModuleName -Times 1 -Exactly -ParameterFilter {
                $Arguments[0] -eq 'run' -and $Arguments -contains 'example.com/team/agent:latest'
            }
        }

        It 'runs the version-pinned default when nothing is configured' {
            Set-BuildAgentConfig -DockerImage $BuildAgentConfig.DockerImage -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            Invoke-Build -type docker

            Should -Invoke Invoke-Docker -ModuleName $script:ModuleName -Times 1 -Exactly -ParameterFilter {
                $Arguments[0] -eq 'run' -and $Arguments -contains "ghcr.io/the-running-dev/build-agent:$script:ModuleVersion"
            }
        }
    }

    Context 'Image cannot be obtained (S9.6)' {
        BeforeEach {
            Set-BuildAgentConfig -DockerImage 'example.com/team/agent:9.9.9' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            Mock Invoke-Docker -ModuleName $script:ModuleName {
                if ($Arguments[0] -eq 'version') { return [pscustomobject]@{ ExitCode = 0; Output = @('27.0.0') } }
                [pscustomobject]@{ ExitCode = 1; Output = @('manifest unknown') }
            }
        }

        It 'fails with ImageUnavailable and exit 5' {
            $failure = Get-Failure { Invoke-Build -type docker }

            $failure | Should -Not -BeNullOrEmpty
            $failure.FullyQualifiedErrorId | Should -BeLike 'ImageUnavailable*'
            $failure.Exception.Data['ExitCode'] | Should -Be 5
        }

        It 'never substitutes another image or runs the build' {
            Get-Failure { Invoke-Build -type docker } | Out-Null

            Should -Invoke Invoke-Docker -ModuleName $script:ModuleName -Times 0 -Exactly -ParameterFilter { $Arguments[0] -eq 'run' }
            Should -Invoke Invoke-Docker -ModuleName $script:ModuleName -Times 0 -Exactly -ParameterFilter {
                $Arguments[0] -eq 'pull' -and $Arguments -notcontains 'example.com/team/agent:9.9.9'
            }
        }
    }

    Context 'Docker unavailable' {
        It 'fails with DockerUnavailable and exit 5' {
            Set-BuildAgentConfig -DockerImage 'example.com/team/agent:1.0.0' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            Mock Invoke-Docker -ModuleName $script:ModuleName { [pscustomobject]@{ ExitCode = 1; Output = @('Cannot connect to the Docker daemon') } }

            $failure = Get-Failure { Invoke-Build -type docker }

            $failure.FullyQualifiedErrorId | Should -BeLike 'DockerUnavailable*'
            $failure.Exception.Data['ExitCode'] | Should -Be 5
        }
    }

    Context 'Workspace (S9.7)' {
        It 'rejects an absent path at configuration with WorkspaceInvalid and exit 3' {
            $missing = Join-Path $script:Workspace 'missing'
            $failure = Get-Failure { Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $missing }

            $failure.FullyQualifiedErrorId | Should -BeLike 'WorkspaceInvalid*'
            $failure.Exception.Data['ExitCode'] | Should -Be 3
        }

        It 'rejects a path that is a file' {
            $file = Join-Path $script:Workspace 'file.txt'
            Set-Content -LiteralPath $file -Value 'x'
            $failure = Get-Failure { Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $file }

            $failure.FullyQualifiedErrorId | Should -BeLike 'WorkspaceInvalid*'
            $failure.Exception.Data['ExitCode'] | Should -Be 3
        }

        It 'fails Invoke-Build with WorkspaceInvalid when the directory has since gone, without touching docker' {
            $gone = Join-Path $script:Workspace 'gone'
            New-Item -ItemType Directory -Path $gone | Out-Null
            Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $gone
            Remove-Item -LiteralPath $gone -Force

            $failure = Get-Failure { Invoke-Build -type docker }

            $failure.FullyQualifiedErrorId | Should -BeLike 'WorkspaceInvalid*'
            $failure.Exception.Data['ExitCode'] | Should -Be 3
            Should -Invoke Invoke-Docker -ModuleName $script:ModuleName -Times 0 -Exactly
        }
    }

    Context 'Build outcome (S9.8)' {
        It 'propagates a non-zero container exit unchanged' -ForEach @(1, 2, 42, 125) {
            $code = $_
            Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            Mock Invoke-Docker -ModuleName $script:ModuleName -ParameterFilter { $Arguments[0] -eq 'run' } -MockWith {
                [pscustomobject]@{ ExitCode = $code; Output = @() }
            }.GetNewClosure()

            $failure = Get-Failure { Invoke-Build -type docker }

            $failure.FullyQualifiedErrorId | Should -BeLike 'BuildFailed*'
            $failure.Exception.Data['ExitCode'] | Should -Be $code
        }

        It 'succeeds silently on exit 0' {
            Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            { Invoke-Build -type docker } | Should -Not -Throw
        }
    }

    Context 'Argument translation (R-INVOKE-003)' {
        BeforeEach {
            $script:RunArgs = $null
            Mock Invoke-Docker -ModuleName $script:ModuleName -ParameterFilter { $Arguments[0] -eq 'run' } -MockWith {
                $script:RunArgs = $Arguments
                [pscustomobject]@{ ExitCode = 0; Output = @() }
            }
        }

        It 'turns <Key> into --<Flag>' -ForEach @(
            @{ Key = 'imageTag'; Flag = 'image-tag' }
            @{ Key = 'ImageTag'; Flag = 'image-tag' }
            @{ Key = 'registryUrl'; Flag = 'registry-url' }
            @{ Key = 'dryRun'; Flag = 'dry-run' }
            @{ Key = 'tags'; Flag = 'tags' }
        ) {
            Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            Invoke-Build -type docker -buildArgs @{ $Key = 'v' }

            $index = [array]::IndexOf($script:RunArgs, "--$Flag")
            $index | Should -BeGreaterThan 0
            $script:RunArgs[$index + 1] | Should -BeExactly 'v'
        }

        It 'repeats the option once per list item' {
            Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
            Invoke-Build -type docker -buildArgs @{ tags = @('a', 'b') }

            ($script:RunArgs -join ' ') | Should -BeLike '*--tags a --tags b*'
        }

        It 'lets buildArgs win over AdditionalParameters' {
            Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace -AdditionalParameters @{ imageTag = 'from-config'; registryUrl = 'ghcr.io/kept' }
            Invoke-Build -type docker -buildArgs @{ imageTag = 'from-call' }

            $joined = $script:RunArgs -join ' '
            $joined | Should -BeLike '*--image-tag from-call*'
            $joined | Should -Not -BeLike '*from-config*'
            $joined | Should -BeLike '*--registry-url ghcr.io/kept*'
        }

        It 'mounts a relative workspace by its absolute path' {
            Push-Location (Split-Path $script:Workspace -Parent)
            try {
                Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath (Split-Path $script:Workspace -Leaf)
            }
            finally { Pop-Location }
            Invoke-Build -type docker

            $BuildAgentConfig.WorkspacePath | Should -Be (Resolve-Path -LiteralPath $script:Workspace).ProviderPath
            $script:RunArgs | Should -Contain "$((Resolve-Path -LiteralPath $script:Workspace).ProviderPath):/workspace"
        }
    }

    Context 'validateArgs (R-VALIDATE-001)' {
        BeforeEach {
            $script:ParametersJson = Join-Path (Split-Path (Get-Module $script:ModuleName).Path -Parent) 'parameters.json'
            $script:SavedParametersJson = "$script:ParametersJson.saved"
            if (Test-Path -LiteralPath $script:ParametersJson) {
                Move-Item -LiteralPath $script:ParametersJson -Destination $script:SavedParametersJson -Force
            }
            '[{"Name":"Docker","Parameters":[{"Name":"imageTag"},{"Name":"registryUrl"}]}]' |
                Set-Content -LiteralPath $script:ParametersJson -Encoding UTF8
            Set-BuildAgentConfig -DockerImage 'a/b:1' -DockerHost 'tcp://localhost:2375' -WorkspacePath $script:Workspace
        }

        AfterEach {
            Remove-Item -LiteralPath $script:ParametersJson -Force
            if (Test-Path -LiteralPath $script:SavedParametersJson) {
                Move-Item -LiteralPath $script:SavedParametersJson -Destination $script:ParametersJson -Force
            }
        }

        It 'rejects an unknown key before docker runs' {
            $failure = Get-Failure { Invoke-Build -type docker -buildArgs @{ notAParameter = 'x' } -validateArgs }

            "$failure" | Should -BeLike '*notAParameter*'
            Should -Invoke Invoke-Docker -ModuleName $script:ModuleName -Times 0 -Exactly
        }

        It 'accepts known keys' {
            { Invoke-Build -type docker -buildArgs @{ imageTag = 'x' } -validateArgs } | Should -Not -Throw
        }
    }
}

Describe 'Exported surface (S9.9)' {
    BeforeAll { Import-FreshModule }

    It 'exports exactly Set-BuildAgentConfig, Invoke-Build and BuildAgentConfig' {
        $module = Get-Module $script:ModuleName
        ($module.ExportedFunctions.Keys | Sort-Object) | Should -Be @('Invoke-Build', 'Set-BuildAgentConfig')
        ($module.ExportedVariables.Keys | Sort-Object) | Should -Be @('BuildAgentConfig')
        $module.ExportedCmdlets.Count | Should -Be 0
        $module.ExportedAliases.Count | Should -Be 0
    }

    It 'keeps the manifest and the module in agreement' {
        $manifest = Test-ModuleManifest -Path $script:ManifestPath
        ($manifest.ExportedFunctions.Keys | Sort-Object) | Should -Be @('Invoke-Build', 'Set-BuildAgentConfig')
        ($manifest.ExportedVariables.Keys | Sort-Object) | Should -Be @('BuildAgentConfig')
    }

    It 'keeps the parameter names' {
        (Get-Command Set-BuildAgentConfig).Parameters.Keys | Should -Contain 'DockerImage'
        (Get-Command Invoke-Build).Parameters.Keys | Should -Contain 'type'
    }
}

# Every exported function must carry comment-based help that Get-Help can render: a synopsis, a
# description, at least one example, and a description for every parameter the function declares.
# The cases come from the manifest and the module source, so a function added later is covered
# without editing this file.
Describe 'Comment-based help' {
    BeforeDiscovery {
        $manifestPath = Join-Path $PSScriptRoot 'Docker-BuildAgent.psd1'
        $modulePath = Join-Path $PSScriptRoot 'Docker-BuildAgent.psm1'
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($modulePath, [ref]$null, [ref]$null)
        $definitions = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true))

        $helpCases = foreach ($name in @((Test-ModuleManifest -Path $manifestPath).ExportedFunctions.Keys | Sort-Object)) {
            $definition = $definitions | Where-Object { $_.Name -eq $name } | Select-Object -First 1
            $declared = @()
            if ($definition -and $definition.Body.ParamBlock) {
                $declared = @($definition.Body.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
            }
            @{
                Name       = $name
                Defined    = [bool]$definition
                Parameters = @($declared | ForEach-Object { @{ Parameter = $_ } })
            }
        }
    }

    Context '<Name>' -ForEach $helpCases {
        BeforeAll {
            Import-FreshModule
            $script:help = Get-Help -Name $Name -Full
        }

        It 'is defined in the module source' {
            $Defined | Should -BeTrue
        }

        It 'has a synopsis' {
            $synopsis = ([string]$script:help.Synopsis).Trim()
            $synopsis | Should -Not -BeNullOrEmpty
            # Without help, Get-Help substitutes the generated syntax, which starts with the command name.
            $synopsis | Should -Not -Match ('^' + [regex]::Escape($Name) + '\s')
        }

        It 'has a description' {
            ($script:help.Description | Out-String).Trim() | Should -Not -BeNullOrEmpty
        }

        It 'has at least one example' {
            # Without help, Get-Help has no examples property to enumerate, so drop the null it yields.
            @($script:help.Examples.Example | Where-Object { $_ }).Count | Should -BeGreaterThan 0
        }

        It 'describes parameter <Parameter>' -ForEach $Parameters {
            $entry = @($script:help.Parameters.Parameter) | Where-Object { $_.Name -eq $Parameter } | Select-Object -First 1
            $entry | Should -Not -BeNullOrEmpty
            ($entry.Description | Out-String).Trim() | Should -Not -BeNullOrEmpty
        }

        It 'documents no parameter the function does not declare' {
            $declared = @($Parameters | ForEach-Object { $_.Parameter.ToUpperInvariant() })
            $definition = (Get-Command -Name $Name -Module $script:ModuleName).ScriptBlock.Ast
            $documented = @($definition.GetHelpContent().Parameters.Keys)
            @($documented | Where-Object { $_ -notin $declared }) | Should -BeNullOrEmpty
        }
    }
}
