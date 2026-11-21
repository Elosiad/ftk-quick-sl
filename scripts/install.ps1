param(
    [string]$GameDir = '',
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'

$buildScript = Join-Path $PSScriptRoot 'build.ps1'
$resolvedGameDir = & $buildScript -GameDir $GameDir -ResolveOnly

$bepCore = Join-Path $resolvedGameDir 'BepInEx\core'
if (-not (Test-Path (Join-Path $bepCore 'BepInEx.dll'))) {
    throw "BepInEx 5.x (win_x64) not found in '$resolvedGameDir'. Please install BepInEx 5.4.x x64 from GitHub releases first."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$builtDll = Join-Path $repoRoot 'dist\FtkQuickSL.dll'
if ($Rebuild -or -not (Test-Path $builtDll)) {
    & $buildScript -GameDir $resolvedGameDir
}

$pluginTargetDir = Join-Path $resolvedGameDir 'BepInEx\plugins\FtkQuickSL'
New-Item -ItemType Directory -Force -Path $pluginTargetDir | Out-Null
$targetDll = Join-Path $pluginTargetDir 'FtkQuickSL.dll'
Copy-Item -Path $builtDll -Destination $targetDll -Force

Write-Host "Installed FtkQuickSL to: $targetDll"
Get-Item $targetDll | Select-Object FullName, Length, LastWriteTime
