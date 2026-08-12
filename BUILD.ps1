<#
.SYNOPSIS
  Build-only script for Erenshor Crafting Expanded. Never installs anything.

.DESCRIPTION
  Locates the installed Erenshor assemblies and the Lunaris developer reference (for
  Lunaris.dll/0Harmony.dll reference purposes only), compiles the mod, and places the output DLL
  under this mod's own bin\ folder. Stops on the first compiler error. Never touches
  <Erenshor>\plugins - use INSTALL_TEST.ps1 for that, separately and explicitly.

.PARAMETER GameDir
  Erenshor install directory. Auto-detected under Program Files/Steam libraries if omitted.

.PARAMETER LunarisLibDir
  A folder containing Lunaris.dll and 0Harmony.dll, used only to source reference DLLs for
  compilation. Defaults to '.\LunarisLibs'. Nothing under this path is modified.

.OUTPUTS
  Prints the exact output DLL path and the exact Erenshor assembly path used.
#>
param(
    [string]$GameDir = "",
    [string]$LunarisLibDir = ""
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Game([string]$Explicit) {
    if ($Explicit -and (Test-Path (Join-Path $Explicit "Erenshor.exe"))) { return (Resolve-Path $Explicit).Path }
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Erenshor" }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles "Steam\steamapps\common\Erenshor" }
    foreach ($drive in @("C","D","E","F")) {
        $candidates += "${drive}:\SteamLibrary\steamapps\common\Erenshor"
    }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate "Erenshor.exe")) { return (Resolve-Path $candidate).Path }
    }
    throw "Erenshor installation not found. Pass -GameDir 'C:\path\to\Erenshor'."
}

function Find-LunarisLibDir([string]$Explicit, [string]$Game) {
    $candidates = @()
    if ($Explicit) { $candidates += $Explicit }
    $candidates += (Join-Path $ScriptRoot "LunarisLibs")
    $candidates += $Game
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (-not $candidate) { continue }
        if ((Test-Path (Join-Path $candidate "Lunaris.dll")) -and (Test-Path (Join-Path $candidate "0Harmony.dll"))) {
            return (Resolve-Path $candidate).Path
        }
    }
    throw "Could not find Lunaris developer references. Put Lunaris.dll and 0Harmony.dll in '$ScriptRoot\LunarisLibs' or pass -LunarisLibDir."
}

function Find-Csc {
    $paths = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
    foreach ($path in $paths) { if (Test-Path $path) { return $path } }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

Write-Host "=== Erenshor Crafting Expanded - build only, no install ===" -ForegroundColor Cyan

$GameDir = Find-Game $GameDir
$LunarisLibDir = Find-LunarisLibDir $LunarisLibDir $GameDir
$csc = Find-Csc
$managed = Join-Path $GameDir "Erenshor_Data\Managed"
$outDir = Join-Path $ScriptRoot "bin"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir "ErenshorCraftingExpanded.dll"

$refs = @(
    (Join-Path $LunarisLibDir "Lunaris.dll"),
    (Join-Path $LunarisLibDir "0Harmony.dll"),
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "netstandard.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.AIModule.dll"),
    (Join-Path $managed "UnityEngine.AnimationModule.dll"),
    (Join-Path $managed "UnityEngine.PhysicsModule.dll"),
    (Join-Path $managed "UnityEngine.UI.dll"),
    (Join-Path $managed "UnityEngine.IMGUIModule.dll"),
    (Join-Path $managed "UnityEngine.TextRenderingModule.dll"),
    (Join-Path $managed "UnityEngine.InputLegacyModule.dll"),
    (Join-Path $managed "UnityEngine.JSONSerializeModule.dll")
)
foreach ($ref in $refs) {
    if (-not (Test-Path $ref)) { throw "Missing reference DLL: $ref" }
}

$sourceFiles = Get-ChildItem (Join-Path $ScriptRoot "src") -Filter "*.cs" -Recurse
if ($sourceFiles.Count -eq 0) { throw "No source files found under src\." }

$rsp = Join-Path $env:TEMP "ErenshorCraftingExpanded.build.rsp"
$lines = @('/nologo', '/target:library', '/optimize+', ('/out:"{0}"' -f $out))
$refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }
$sourceFiles | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
$lines | Set-Content $rsp -Encoding ASCII

Write-Host "Erenshor assemblies: $managed"
Write-Host "Lunaris refs:        $LunarisLibDir"
Write-Host "Compiling $($sourceFiles.Count) source file(s)..."

& $csc "@$rsp"
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed (csc exit code $LASTEXITCODE). No install attempted - there is nothing to install."
}

Write-Host ""
Write-Host "BUILD OK" -ForegroundColor Green
Write-Host "Output DLL:        $out"
Write-Host "Erenshor assembly: $(Join-Path $managed 'Assembly-CSharp.dll')"
Write-Host ""
Write-Host "Nothing was installed. Run INSTALL_TEST.ps1 separately and explicitly to test it in-game."
