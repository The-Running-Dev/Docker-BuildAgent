[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../")).Path,
    [string]$ModulePath = $PSScriptRoot,
    [string]$OutputFile
)

if (-not $OutputFile) {
    $OutputFile = Join-Path $ModulePath "parameters.json"
}

function Get-ParameterFiles {
    param([string]$RootPath)
    $paramDir = Join-Path $RootPath "forge/Common/Parameters"
    if (-not (Test-Path $paramDir)) {
        throw "Parameter directory not found: $paramDir"
    }
    return Get-ChildItem -Path $paramDir -Filter "*.cs" -Recurse
}

function Parse-ParameterFileWithXmlDoc {
    param($File)
    $content = Get-Content $File.FullName -Raw
    
    $classNameMatch = $content | Select-String -Pattern 'public class (\w+)'
    if (-not $classNameMatch) { return $null }
    $className = $classNameMatch.Matches.Groups[1].Value

    # Supports nullable/reference/value types, arrays, and simple generic forms such as List<string>.
    $propertyRegex = [regex]'(?s)/// <summary>(.*?)</summary>.*?public\s+([\w\.\?\[\]<> ,]+?)\s+(\w+)\s*\{\s*get;\s*set;\s*\}'
    $matches = $propertyRegex.Matches($content)

    $params = @()
    foreach ($match in $matches) {
        $summary = $match.Groups[1].Value.Trim() -replace '\s+', ' ' -replace '///', ''
        $type = $match.Groups[2].Value
        $name = $match.Groups[3].Value
        
        $params += @{
            Name = $name
            Type = $type
            Description = $summary.Trim()
        }
    }

    # Handle inheritance
    $inheritanceMatch = $content | Select-String -Pattern "public class \w+ : (\w+)"
    $baseClassName = if ($inheritanceMatch) { $inheritanceMatch.Matches.Groups[1].Value } else { $null }

    return @{
        Name = $className -replace "Params", ""
        Base = $baseClassName -replace "Params", ""
        Parameters = $params
    }
}

Write-Host "Starting parameter extraction..."
$allConfigs = @{}
$paramFiles = Get-ParameterFiles -RootPath $ProjectRoot

foreach ($file in $paramFiles) {
    Write-Host "Parsing $($file.Name)..."
    $config = Parse-ParameterFileWithXmlDoc -File $file
    if ($config) {
        $allConfigs[$config.Name] = $config
    }
}

# Resolve inheritance recursively so multi-level chains are fully merged.
$resolvedParameters = @{}

function Resolve-ConfigParameters {
    param(
        [string]$ConfigName,
        [System.Collections.Generic.HashSet[string]]$Stack
    )

    if ($resolvedParameters.ContainsKey($ConfigName)) {
        return $resolvedParameters[$ConfigName]
    }

    if (-not $allConfigs.ContainsKey($ConfigName)) {
        return @()
    }

    if ($Stack.Contains($ConfigName)) {
        throw "Circular inheritance detected while resolving '$ConfigName'."
    }

    $null = $Stack.Add($ConfigName)

    $config = $allConfigs[$ConfigName]
    $baseParams = @()
    if ($config.Base) {
        $baseParams = Resolve-ConfigParameters -ConfigName $config.Base -Stack $Stack
    }

    $null = $Stack.Remove($ConfigName)

    $merged = @($baseParams + $config.Parameters)
    $resolvedParameters[$ConfigName] = $merged

    return $merged
}

foreach ($configName in $allConfigs.Keys) {
    $config = $allConfigs[$configName]
    $config.Parameters = Resolve-ConfigParameters -ConfigName $configName -Stack ([System.Collections.Generic.HashSet[string]]::new())
}

$allConfigs.Values | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputFile

Write-Host "[OK] Module parameters extracted to $OutputFile" -ForegroundColor Green
