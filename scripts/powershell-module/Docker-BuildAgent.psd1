@{
    RootModule           = 'Docker-BuildAgent.psm1'
    ModuleVersion        = '2.0.0'
    GUID                 = 'e8fca2e5-1d9a-4a1b-9b3e-7b6b3b4b6b1b'
    Author               = 'The-Running-Dev'
    CompanyName          = 'The-Running-Dev'
    Copyright            = '(c) 2025 The-Running-Dev. All rights reserved.'
    Description          = 'A PowerShell module for orchestrating Docker-based builds with the Docker-BuildAgent.'
    PowerShellVersion    = '5.1'
    DotNetFrameworkVersion = '4.7.2'
    CLRVersion           = '4.0'
    ProcessorArchitecture  = 'Amd64'
    RequiredModules      = @()
    RequiredAssemblies   = @()
    ScriptsToProcess     = @()
    TypesToProcess       = @()
    FormatsToProcess     = @()
    NestedModules        = @()
    FunctionsToExport    = '*'
    CmdletsToExport      = '*'
    VariablesToExport    = '*'
    AliasesToExport      = '*'
    PrivateData          = @{
        PSData = @{
            ReleaseNotes = @(
                'Initial release of the module.'
            )
        }
    }
}
