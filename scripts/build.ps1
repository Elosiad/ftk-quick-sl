param(
    [string]$GameDir = '',
    [string]$OutDir = '',
    [switch]$ResolveOnly
)

$ErrorActionPreference = 'Stop'

function Resolve-FtkGameDir {
    param([string]$ExplicitDir)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitDir)) {
        $asm = Join-Path $ExplicitDir 'FTK_Data\Managed\Assembly-CSharp.dll'
        if (Test-Path $asm) { return (Resolve-Path $ExplicitDir).Path }
        throw "Assembly-CSharp.dll not found under specified GameDir: $ExplicitDir"
    }

    $candidates = New-Object System.Collections.Generic.List[string]

    $steamRoots = New-Object System.Collections.Generic.List[string]
    foreach ($regInfo in @(
        @{ Path = 'HKCU:\Software\Valve\Steam'; Name = 'SteamPath' },
        @{ Path = 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam'; Name = 'InstallPath' },
        @{ Path = 'HKLM:\SOFTWARE\Valve\Steam'; Name = 'InstallPath' }
    )) {
        try {
            if (Test-Path $regInfo.Path) {
                $val = (Get-ItemProperty -Path $regInfo.Path -Name $regInfo.Name -ErrorAction SilentlyContinue).($regInfo.Name)
                if ($val) {
                    $steamRoots.Add(($val -replace '/', '\'))
                }
            }
        } catch { }
    }

    foreach ($root in ($steamRoots | Select-Object -Unique)) {
        $candidates.Add((Join-Path $root 'steamapps\common\For The King'))
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            $content = Get-Content $vdf -Raw -ErrorAction SilentlyContinue
            if ($content) {
                foreach ($m in [regex]::Matches($content, '"path"\s+"([^"]+)"')) {
                    $libPath = $m.Groups[1].Value.Replace('\\', '\')
                    $candidates.Add((Join-Path $libPath 'steamapps\common\For The King'))
                }
                foreach ($m in [regex]::Matches($content, '"\d+"\s+"([A-Za-z]:[^"]+)"')) {
                    $libPath = $m.Groups[1].Value.Replace('\\', '\')
                    $candidates.Add((Join-Path $libPath 'steamapps\common\For The King'))
                }
            }
        }
    }

    foreach ($drive in @('C', 'D', 'E', 'F', 'G')) {
        $candidates.Add("${drive}:\Program Files (x86)\Steam\steamapps\common\For The King")
        $candidates.Add("${drive}:\Program Files\Steam\steamapps\common\For The King")
        $candidates.Add("${drive}:\SteamLibrary\steamapps\common\For The King")
        $candidates.Add("${drive}:\Steam\steamapps\common\For The King")
    }

    foreach ($cand in ($candidates | Select-Object -Unique)) {
        $asm = Join-Path $cand 'FTK_Data\Managed\Assembly-CSharp.dll'
        if (Test-Path $asm) {
            return (Resolve-Path $cand).Path
        }
    }

    throw "Could not auto-detect 'For The King' installation directory. Pass -GameDir '<path-to-For The King>' explicitly."
}

$resolvedGameDir = Resolve-FtkGameDir -ExplicitDir $GameDir
if ($ResolveOnly) {
    Write-Output $resolvedGameDir
    return
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutDir)) {
    $OutDir = Join-Path $repoRoot 'dist'
}
$srcDir = Join-Path $repoRoot 'src'

$cscCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "csc.exe (.NET Framework v4.0.30319) not found on this machine."
}

$managed = Join-Path $resolvedGameDir 'FTK_Data\Managed'
$bepCore = Join-Path $resolvedGameDir 'BepInEx\core'
if (-not (Test-Path (Join-Path $bepCore 'BepInEx.dll'))) {
    throw "BepInEx.dll not found in '$bepCore'. Install BepInEx 5.x (win_x64) into For The King first."
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$outDll = Join-Path $OutDir 'FtkQuickSL.dll'

$refs = @(
    (Join-Path $managed 'mscorlib.dll'),
    (Join-Path $managed 'System.dll'),
    (Join-Path $managed 'System.Core.dll'),
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'Assembly-CSharp-firstpass.dll'),
    (Join-Path $managed 'PlayMaker.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'UnityEngine.CoreModule.dll'),
    (Join-Path $managed 'UnityEngine.InputModule.dll'),
    (Join-Path $managed 'UnityEngine.UI.dll'),
    (Join-Path $managed 'UnityEngine.UIModule.dll'),
    (Join-Path $managed 'UnityEngine.AnimationModule.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'),
    (Join-Path $bepCore 'BepInEx.dll'),
    (Join-Path $bepCore 'BepInEx.Harmony.dll'),
    (Join-Path $bepCore '0Harmony.dll')
)

foreach ($r in $refs) {
    if (-not (Test-Path $r)) { throw "Missing reference DLL: $r" }
}

$srcFiles = Get-ChildItem -Path $srcDir -Filter '*.cs' | Select-Object -ExpandProperty FullName
if ($srcFiles.Count -eq 0) { throw "No .cs source files found under $srcDir." }

$cscArgs = @(
    '/nologo',
    '/target:library',
    '/optimize+',
    '/langversion:5',
    '/nostdlib',
    '/noconfig',
    '/utf8output',
    "/out:$outDll"
)
foreach ($r in $refs) {
    $cscArgs += "/reference:$r"
}
$cscArgs += $srcFiles

Write-Host "GameDir : $resolvedGameDir"
Write-Host "Compiling FtkQuickSL.dll (.NET 3.5 / Mono target)..."
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed with exit code $LASTEXITCODE"
}

$resolver = [System.ResolveEventHandler]{
    param($s, $e)
    $name = (New-Object System.Reflection.AssemblyName($e.Name)).Name + '.dll'
    foreach ($dir in @($managed, $bepCore)) {
        $p = Join-Path $dir $name
        if (Test-Path $p) { return [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($p) }
    }
    return $null
}
[System.AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
$builtAsm = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($outDll)
Write-Host "Built: $outDll ($((Get-Item $outDll).Length) bytes)"
Write-Host "ImageRuntimeVersion: $($builtAsm.ImageRuntimeVersion)"
foreach ($refAsm in $builtAsm.GetReferencedAssemblies()) {
    if ($refAsm.Name -eq 'mscorlib' -and $refAsm.Version.Major -ge 4) {
        throw "CRITICAL: mscorlib v$($refAsm.Version) referenced! Expected 2.0.0.0."
    }
}
Write-Host "Build and .NET 3.5 verification succeeded."
