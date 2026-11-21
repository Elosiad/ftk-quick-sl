param(
    [string]$GameDir = ''
)

$ErrorActionPreference = 'Stop'

$buildScript = Join-Path $PSScriptRoot 'build.ps1'
$resolvedGameDir = & $buildScript -GameDir $GameDir -ResolveOnly

$pluginDir = Join-Path $resolvedGameDir 'BepInEx\plugins\FtkQuickSL'
if (Test-Path $pluginDir) {
    Remove-Item -Path $pluginDir -Recurse -Force
    Write-Host "Removed plugin directory: $pluginDir"
} else {
    Write-Host "Plugin directory not found: $pluginDir"
}
