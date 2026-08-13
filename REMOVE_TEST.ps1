<#
.SYNOPSIS
  Undoes an INSTALL_TEST.ps1 session for Erenshor Crafting Expanded.

.DESCRIPTION
  Removes <GameDir>\plugins\ErenshorCraftingExpanded.dll, then finds the most recent
  UNRESTORED backup session (under test-backups\) whose target-root.txt matches this exact
  GameDir, and restores its prior state: if that session had a prior install, the backed-up DLL
  is copied back; if it had none, the target is simply left removed. The session is then marked
  restored.txt so it cannot be replayed a second time.

.PARAMETER GameDir
  Erenshor install directory. Auto-detected if omitted.
#>
param(
    [string]$GameDir = ""
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

Write-Host "=== Erenshor Crafting Expanded - remove TEST install ===" -ForegroundColor Cyan

$GameDir = Find-Game $GameDir
Write-Host "Target Erenshor install: $GameDir"

$installedDll = Join-Path (Join-Path $GameDir "plugins") "ErenshorCraftingExpanded.dll"
if (Test-Path $installedDll) {
    Remove-Item $installedDll -Force
    Write-Host "Removed: $installedDll"
} else {
    Write-Host "Nothing currently installed at: $installedDll"
}

$backupsRoot = Join-Path $ScriptRoot "test-backups"
if (-not (Test-Path $backupsRoot)) {
    Write-Host "No test-backups\ directory found - nothing to restore."
    return
}

$sessions = Get-ChildItem $backupsRoot -Directory | Sort-Object Name -Descending
$targetSession = $null
foreach ($session in $sessions) {
    $restoredMarker = Join-Path $session.FullName "restored.txt"
    if (Test-Path $restoredMarker) { continue }
    $rootFile = Join-Path $session.FullName "target-root.txt"
    if (-not (Test-Path $rootFile)) { continue }
    $recordedRoot = (Get-Content $rootFile -Raw).Trim()
    if ($recordedRoot -ieq $GameDir) { $targetSession = $session; break }
}

if (-not $targetSession) {
    Write-Host "No unrestored INSTALL_TEST.ps1 backup session found for this GameDir - nothing to restore."
    return
}

$hadPriorInstallFile = Join-Path $targetSession.FullName "had-prior-install.txt"
$hadPriorInstall = $false
if (Test-Path $hadPriorInstallFile) {
    $hadPriorInstall = [bool]::Parse((Get-Content $hadPriorInstallFile -Raw).Trim())
}

if ($hadPriorInstall) {
    $backupDll = Join-Path $targetSession.FullName "ErenshorCraftingExpanded.dll.bak"
    if (Test-Path $backupDll) {
        New-Item -ItemType Directory -Force -Path (Join-Path $GameDir "plugins") | Out-Null
        Copy-Item $backupDll $installedDll -Force
        Write-Host "Restored prior install from: $backupDll"
    } else {
        Write-Host "Session claims a prior install existed but its backup file is missing: $backupDll" -ForegroundColor Yellow
    }
} else {
    Write-Host "Session recorded no prior install - target correctly left removed."
}

Set-Content -Path (Join-Path $targetSession.FullName "restored.txt") -Value (Get-Date -Format "o") -Encoding UTF8
Write-Host ""
Write-Host "REMOVE TEST OK" -ForegroundColor Green
Write-Host "  Session restored: $($targetSession.Name)"
