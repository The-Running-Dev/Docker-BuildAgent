[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../")).Path,
    [string]$ModulePath = $PSScriptRoot,
    [string]$OutputFile = (Join-Path $PSScriptRoot "parameters.json")
)

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

    $propertyRegex = [regex]'(?s)/// <summary>(.*?)</summary>.*?public (\w+) (\w+) \{ get; set; \}'
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

# Combine base class parameters
foreach ($config in $allConfigs.Values) {
    if ($config.Base -and $allConfigs.ContainsKey($config.Base)) {
        $baseConfig = $allConfigs[$config.Base]
        $config.Parameters = $baseConfig.Parameters + $config.Parameters
    }
}

$allConfigs.Values | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputFile

Write-Host "[OK] Module parameters extracted to $OutputFile" -ForegroundColor Green
