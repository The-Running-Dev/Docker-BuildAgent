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
