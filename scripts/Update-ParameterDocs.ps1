#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Regenerates the per-build-type parameter tables in documentation/docs/parameters.md.

.DESCRIPTION
    Reads the `*Params` classes (forge/Common/Parameters) and the NUKE `[Parameter]` fields declared by
    forge/Common/Base.cs and each build type, and writes one table per build type with the columns
    flag, environment variable, config key and default. Nothing in the tables is typed by hand.

    The tables sit under the headings named in $Targets. The script replaces the first table below each
    heading and leaves every other line of the page alone. The page keeps its own line endings.

    A `*Params` property becomes a row. Its flag and environment variable exist only when the build type
    declares a `[Parameter]` field of the same name; its config key is the kebab-case property name
    (design/20-contract.md, I17). A `[Parameter]` field with no property is a row only when the build type
    reads it directly (see $ReadDirectly); any other such field is reported and left out because the build
    never reads it.

.PARAMETER Check
    Writes nothing. Exits 1 when the page differs from what the sources produce.

.PARAMETER Path
    The page to update. Defaults to documentation/docs/parameters.md.

.EXAMPLE
    ./scripts/Update-ParameterDocs.ps1

.EXAMPLE
    ./scripts/Update-ParameterDocs.ps1 -Check
#>
[CmdletBinding()]
param(
    [switch]$Check,
    [string]$Path
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $Path) { $Path = Join-Path $root 'documentation/docs/parameters.md' }

# One entry per build type: the page heading the table follows, the Params class, the file declaring
# the [Parameter] fields, and the NUKE parameters the build type reads without a Params property.
$Targets = @(
    @{ Heading = '## Forge';          Params = 'ForgeParams';         Source = 'forge/Forge/Forge.cs';                ReadDirectly = @('ChangeLogSource') }
    @{ Heading = '## Docker';         Params = 'DockerParams';        Source = 'forge/Docker/Docker.cs';              ReadDirectly = @() }
    @{ Heading = '## Node';           Params = 'NodeParams';          Source = 'forge/Node/Node.cs';                  ReadDirectly = @() }
    @{ Heading = '## Node in Docker'; Params = 'NodeInDockerParams';  Source = 'forge/NodeInDocker/NodeInDocker.cs'; ReadDirectly = @() }
)

# Parameters NUKE itself supplies to every build type (not declared in this repository's sources).
$NukeParameters = @{
    Verbosity = @{ Default = 'Normal' }
}

# Properties whose value the build computes; they have no scalar default to show.
$Computed = @('Config', 'RootDirectory', 'RepositoryUrl', 'Version', 'ChangeLogConfig', 'Tags', 'ReleaseTag')

function ConvertTo-Kebab([string]$Name) {
    return ([regex]::Replace($Name, '([a-z])([A-Z])', '$1-$2')).ToLowerInvariant()
}

function ConvertTo-DefaultText([string]$Initializer) {
    if (-not $Initializer) { return $null }
    $value = $Initializer.Trim()
    if ($value -eq 'string.Empty' -or $value -eq '""') { return '' }
    if ($value -match '^"(.*)"$') { return $Matches[1] }
    if ($value -in @('true', 'false')) { return $value }
    if ($value -eq 'null') { return $null }
    return $value
}

function Read-ParamsClasses {
    $classes = @{}
    foreach ($file in Get-ChildItem (Join-Path $root 'forge/Common/Parameters') -Filter '*Params.cs') {
        $current = $null
        $secret = $false
        foreach ($line in Get-Content $file.FullName) {
            if ($line -match 'public class (\w+)(?:\s*:\s*(\w+))?') {
                $current = @{ Name = $Matches[1]; Base = $Matches[2]; Properties = @() }
                $classes[$current.Name] = $current
                $secret = $false
                continue
            }
            if (-not $current) { continue }
            if ($line -match '^\s*\[SecretParameter\]') { $secret = $true; continue }
            if ($line -match 'public\s+([\w<>?\.]+)\s+(\w+)\s*\{\s*get;\s*set;\s*\}(?:\s*=\s*([^;]+);)?') {
                $current.Properties += @{
                    Name        = $Matches[2]
                    Type        = $Matches[1]
                    Initializer = $Matches[3]
                    Secret      = $secret
                }
                $secret = $false
            }
        }
    }
    return $classes
}

function Read-NukeFields([string]$RelativePath) {
    $fields = @{}
    $lines = Get-Content (Join-Path $root $RelativePath)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -notmatch '^\s*\[Parameter\(') { continue }
        for ($j = $i + 1; $j -lt $lines.Count; $j++) {
            if ($lines[$j] -match 'public\s+(?:readonly\s+)?([\w<>?\.]+)\s+(\w+)\s*(?:=\s*([^;]+))?;') {
                $fields[$Matches[2]] = @{ Name = $Matches[2]; Type = $Matches[1]; Initializer = $Matches[3] }
                break
            }
        }
    }
    return $fields
}

function Get-Rows($Target, $Classes) {
    $chain = @()
    for ($name = $Target.Params; $name; $name = $Classes[$name].Base) { $chain = , $Classes[$name] + $chain }

    $fields = Read-NukeFields 'forge/Common/Base.cs'
    foreach ($entry in (Read-NukeFields $Target.Source).GetEnumerator()) { $fields[$entry.Key] = $entry.Value }

    $rows = @()
    $seen = @{}
    foreach ($class in $chain) {
        foreach ($property in $class.Properties) {
            if ($seen.ContainsKey($property.Name)) { continue }
            $seen[$property.Name] = $true

            $field = $fields[$property.Name]
            $nuke = $NukeParameters[$property.Name]
            $declared = ConvertTo-DefaultText $property.Initializer
            if ($field -and $null -ne $field.Initializer) { $declared = ConvertTo-DefaultText $field.Initializer }
            if ($nuke) { $declared = $nuke.Default }

            if ($property.Name -in $Computed) { $default = 'set by the build' }
            elseif ($null -eq $declared -and $property.Type -eq 'bool') { $default = 'false' }
            elseif ($null -eq $declared) { $default = 'none' }
            elseif ($declared -eq '') { $default = 'empty' }
            else { $default = $declared }

            $hasFlag = [bool]($field -or $nuke)
            $rows += [pscustomobject]@{
                Computed = ($property.Name -in $Computed)
                Flag   = if ($hasFlag) { '`--' + (ConvertTo-Kebab $property.Name) + '`' } else { 'none' }
                Env    = if ($hasFlag) { '`' + $property.Name + '`' } else { 'none' }
                Key    = if ($property.Secret) { 'none (secret)' } else { '`' + (ConvertTo-Kebab $property.Name) + '`' }
                Default = if ($default -match '^(none|empty|set by the build)$') { $default } else { '`' + $default + '`' }
            }
        }
    }

    foreach ($field in $fields.Values) {
        if ($seen.ContainsKey($field.Name)) { continue }
        if ($field.Name -in $Target.ReadDirectly) {
            $declared = ConvertTo-DefaultText $field.Initializer
            $rows += [pscustomobject]@{
                Computed = $false
                Flag    = '`--' + (ConvertTo-Kebab $field.Name) + '`'
                Env     = '`' + $field.Name + '`'
                Key     = 'none'
                Default = if ($declared) { '`' + $declared + '`' } else { 'none' }
            }
        }
        elseif ($Target.Source -match 'forge/Common/') {
            continue
        }
        else {
            [Console]::Error.WriteLine("note: $($Target.Params): [Parameter] field '$($field.Name)' has no Params property and is not read; left out")
        }
    }
    # Settings you can set come first; the values the build computes itself come last.
    return @($rows | Where-Object { -not $_.Computed }) + @($rows | Where-Object { $_.Computed })
}

function ConvertTo-Table($Rows) {
    $header = @('Flag', 'Environment variable', 'Config key', 'Default')
    $lines = @(
        '| ' + ($header -join ' | ') + ' |'
        '|---|---|---|---|'
    )
    foreach ($row in $Rows) { $lines += "| $($row.Flag) | $($row.Env) | $($row.Key) | $($row.Default) |" }
    return $lines
}

$classes = Read-ParamsClasses
$bytes = [System.IO.File]::ReadAllBytes($Path)
$text = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes)
$newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
$lines = [System.Collections.Generic.List[string]]::new()
foreach ($l in ($text -replace "`r`n", "`n").Split("`n")) { $lines.Add($l) }

foreach ($target in $Targets) {
    $heading = $lines.IndexOf($target.Heading)
    if ($heading -lt 0) { throw "Heading '$($target.Heading)' not found in $Path" }

    $start = -1
    for ($i = $heading + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^## ') { break }
        if ($lines[$i] -like '|*') { $start = $i; break }
    }
    if ($start -lt 0) { throw "No table under '$($target.Heading)' in $Path" }

    $end = $start
    while ($end + 1 -lt $lines.Count -and $lines[$end + 1] -like '|*') { $end++ }

    $table = ConvertTo-Table (Get-Rows $target $classes)
    $lines.RemoveRange($start, $end - $start + 1)
    $lines.InsertRange($start, [string[]]$table)
}

$updated = ($lines -join $newline)
if ($Check) {
    if ($updated -ne $text) {
        [Console]::Error.WriteLine("$Path is out of date. Run scripts/Update-ParameterDocs.ps1.")
        exit 1
    }
    Write-Host "$Path is up to date."
    exit 0
}

[System.IO.File]::WriteAllBytes($Path, [System.Text.UTF8Encoding]::new($false).GetBytes($updated))
Write-Host "Updated $Path"
