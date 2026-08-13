<#
.SYNOPSIS
  Reversible test install for Erenshor Crafting Expanded. Backs up any prior install first.

.DESCRIPTION
  Resolves the target Erenshor install BEFORE building (so BUILD.ps1 always compiles against the
  same install's assemblies), builds, then backs up whatever DLL currently exists at
  <GameDir>\plugins\ErenshorCraftingExpanded.dll (if any) into a timestamped session folder under
  test-backups\, records enough metadata to restore or reason about that session later, and only
  then copies the newly built DLL into place. Use REMOVE_TEST.ps1 to undo this.

.PARAMETER GameDir
  Erenshor install directory. Auto-detected if omitted.

.PARAMETER LunarisLibDir
  Folder containing Lunaris.dll/0Harmony.dll for compilation references. Auto-detected if omitted.
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
    foreach ($drive in @("C","D","E","F")) { $candidates += "${drive}:\SteamLibrary\steamapps\common\Erenshor" }
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate "Erenshor.exe")) { return (Resolve-Path $candidate).Path }
    }
    throw "Erenshor installation not found. Pass -GameDir 'C:\path\to\Erenshor'."
}

Write-Host "=== Erenshor Crafting Expanded - reversible TEST install ===" -ForegroundColor Cyan

# Resolve the target BEFORE building, so the compiled DLL always matches the assemblies of the
# install we are about to touch - never a different profile's references.
$GameDir = Find-Game $GameDir
Write-Host "Target Erenshor install: $GameDir"

& (Join-Path $ScriptRoot "BUILD.ps1") -GameDir $GameDir -LunarisLibDir $LunarisLibDir
if ($LASTEXITCODE -ne 0) { throw "Build failed; nothing installed, nothing backed up." }

$builtDll = Join-Path $ScriptRoot "bin\ErenshorCraftingExpanded.dll"
if (-not (Test-Path $builtDll)) { throw "Expected build output not found: $builtDll" }

$pluginsDir = Join-Path $GameDir "plugins"
New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
$installedDll = Join-Path $pluginsDir "ErenshorCraftingExpanded.dll"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionDir = Join-Path (Join-Path $ScriptRoot "test-backups") $timestamp
New-Item -ItemType Directory -Force -Path $sessionDir | Out-Null

$hadPriorInstall = Test-Path $installedDll
Set-Content -Path (Join-Path $sessionDir "target-root.txt") -Value $GameDir -Encoding UTF8
Set-Content -Path (Join-Path $sessionDir "had-prior-install.txt") -Value $hadPriorInstall.ToString() -Encoding UTF8

if ($hadPriorInstall) {
    Copy-Item $installedDll (Join-Path $sessionDir "ErenshorCraftingExpanded.dll.bak") -Force
    Write-Host "Backed up existing install to: $sessionDir\ErenshorCraftingExpanded.dll.bak"
} else {
    Write-Host "No prior install found at $installedDll - this session records a clean 'no prior DLL' state."
}

Copy-Item $builtDll $installedDll -Force

Write-Host ""
Write-Host "TEST INSTALL OK" -ForegroundColor Green
Write-Host "  Installed: $installedDll"
Write-Host "  Backup session: $sessionDir"
Write-Host ""
Write-Host "Run REMOVE_TEST.ps1 (same -GameDir) when you are done testing to restore the prior state."
