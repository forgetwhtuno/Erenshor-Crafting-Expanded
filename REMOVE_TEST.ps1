<#
.SYNOPSIS
  Safely undo the newest matching INSTALL_TEST.ps1 session for Crafting Expanded.

.DESCRIPTION
  Finds the newest unrestored backup session bound to the exact Erenshor install. If the test DLL
  is still present, its SHA-256 must match the session's installed-sha256.txt before this script
  removes or replaces it. A prior install is restored only when its backup hash matches the recorded
  prior-sha256.txt. This prevents a stale test-cleanup action from deleting a later local build.
#>
param([string]$GameDir = "")

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptRoot 'BuildSupport.ps1')

Write-Host '=== Erenshor Crafting Expanded - safe TEST restore ===' -ForegroundColor Cyan
$GameDir = Resolve-CraftingGameDir $GameDir
$installedDll = Join-Path (Join-Path $GameDir 'plugins') 'ErenshorCraftingExpanded.dll'
$backupsRoot = Join-Path $ScriptRoot 'test-backups'
if (-not (Test-Path $backupsRoot)) { Write-Host 'No test-backups directory found.'; return }

$targetSession = $null
foreach ($session in (Get-ChildItem -LiteralPath $backupsRoot -Directory | Sort-Object Name -Descending)) {
    if (Test-Path (Join-Path $session.FullName 'restored.txt')) { continue }
    $rootFile = Join-Path $session.FullName 'target-root.txt'
    if (-not (Test-Path $rootFile)) { continue }
    $recordedRoot = (Get-Content -LiteralPath $rootFile -Raw).Trim()
    if ($recordedRoot -ieq $GameDir) { $targetSession = $session; break }
}
if (-not $targetSession) { Write-Host 'No unrestored test session found for this Erenshor install.'; return }

$installedHashFile = Join-Path $targetSession.FullName 'installed-sha256.txt'
$expectedTestHash = if (Test-Path $installedHashFile) { (Get-Content -LiteralPath $installedHashFile -Raw).Trim().ToLowerInvariant() } else { '' }
if (Test-Path $installedDll) {
    $currentHash = Get-CraftingSha256 $installedDll
    if (-not $expectedTestHash) { throw 'Backup session has no recorded test DLL hash; refusing to remove the current install.' }
    if ($currentHash -ne $expectedTestHash) {
        throw "Current installed DLL differs from this test session (current=$currentHash expected=$expectedTestHash). Refusing to overwrite/delete it."
    }
}

$hadPriorFile = Join-Path $targetSession.FullName 'had-prior-install.txt'
$hadPrior = $false
if (Test-Path $hadPriorFile) { $hadPrior = [bool]::Parse((Get-Content -LiteralPath $hadPriorFile -Raw).Trim()) }

if ($hadPrior) {
    $backupDll = Join-Path $targetSession.FullName 'ErenshorCraftingExpanded.dll.bak'
    $priorHashFile = Join-Path $targetSession.FullName 'prior-sha256.txt'
    if (-not (Test-Path $backupDll) -or -not (Test-Path $priorHashFile)) { throw 'Prior-install backup metadata is incomplete; refusing restore.' }
    $expectedPriorHash = (Get-Content -LiteralPath $priorHashFile -Raw).Trim().ToLowerInvariant()
    $backupHash = Get-CraftingSha256 $backupDll
    if ($backupHash -ne $expectedPriorHash) { throw 'Prior-install backup hash verification failed; refusing restore.' }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $installedDll) | Out-Null
    Copy-Item -LiteralPath $backupDll -Destination $installedDll -Force
    $restoredHash = Get-CraftingSha256 $installedDll
    if ($restoredHash -ne $expectedPriorHash) { throw 'Restored DLL hash verification failed.' }
    Write-Host "Restored prior DLL SHA-256: $restoredHash" -ForegroundColor Green
}
else {
    if (Test-Path $installedDll) { Remove-Item -LiteralPath $installedDll -Force }
    if (Test-Path $installedDll) { throw 'Test DLL removal failed.' }
    Write-Host 'Test DLL removed; session had no prior install.' -ForegroundColor Green
}

Set-Content -LiteralPath (Join-Path $targetSession.FullName 'restored.txt') -Value (Get-Date -Format 'o') -Encoding UTF8
Write-Host "REMOVE TEST OK: $($targetSession.Name)" -ForegroundColor Green
