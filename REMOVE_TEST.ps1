<#
.SYNOPSIS
  Removes a Crafting Expanded test install and restores the latest UNRESTORED backup session for
  the same BepInEx target, if that session contained a prior install.

.DESCRIPTION
  Deletes ONLY <BepInExRoot>\BepInEx\plugins\ErenshorCraftingExpanded and this mod's config file.
  Backup sessions are target-bound by target-root.txt. A session is marked restored after use so
  running REMOVE_TEST.ps1 twice cannot repeatedly resurrect an older Crafting Expanded build.
  Never touches any other plugin/config file.
#>
param(
    [string]$BepInExRoot = ""
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PluginFolderName = "ErenshorCraftingExpanded"
$ConfigFileName = "forgetwhtuno.erenshor.craftingexpanded.cfg"

function Find-Roots([string]$Explicit) {
    if ($Explicit) {
        if (-not (Test-Path (Join-Path $Explicit "BepInEx"))) {
            throw "-BepInExRoot '$Explicit' does not contain a BepInEx folder."
        }
        return ,(Resolve-Path $Explicit).Path
    }
    $roots = @()
    $profiles = Join-Path $env:APPDATA "r2modmanPlus-local\Erenshor\profiles"
    if (Test-Path $profiles) {
        Get-ChildItem $profiles -Directory | ForEach-Object {
            if (Test-Path (Join-Path $_.FullName "BepInEx\core\BepInEx.dll")) { $roots += $_.FullName }
        }
    }
    return @($roots | Select-Object -Unique)
}

function Find-LatestMatchingBackup([string]$BackupsRoot, [string]$TargetRoot) {
    if (-not (Test-Path $BackupsRoot)) { return $null }
    foreach ($candidate in (Get-ChildItem $BackupsRoot -Directory | Sort-Object Name -Descending)) {
        if (Test-Path (Join-Path $candidate.FullName "restored.txt")) { continue }
        $targetFile = Join-Path $candidate.FullName "target-root.txt"
        if (-not (Test-Path $targetFile)) { continue } # old pre-metadata backups are never guessed
        $recorded = (Get-Content $targetFile -Raw).Trim()
        if ($recorded -eq $TargetRoot) { return $candidate }
    }
    return $null
}

Write-Host "=== Erenshor Crafting Expanded - REMOVE test install ===" -ForegroundColor Cyan

$roots = @(Find-Roots $BepInExRoot)
if ($roots.Count -eq 0) { throw "No BepInEx profile found. Pass -BepInExRoot explicitly." }
if ($roots.Count -gt 1 -and -not $BepInExRoot) {
    Write-Host "Multiple BepInEx profiles found - refusing to guess. Re-run with -BepInExRoot pointing at exactly one:" -ForegroundColor Red
    $roots | ForEach-Object { Write-Host "  $_" }
    throw "Ambiguous target."
}
$TargetRoot = if ($BepInExRoot) { (Resolve-Path $BepInExRoot).Path } else { $roots[0] }
$targetPluginDir = Join-Path $TargetRoot "BepInEx\plugins\$PluginFolderName"
$targetConfigFile = Join-Path $TargetRoot "BepInEx\config\$ConfigFileName"

Write-Host "Target: $TargetRoot"

$removed = @()
if (Test-Path $targetPluginDir) { Remove-Item $targetPluginDir -Recurse -Force; $removed += $targetPluginDir }
if (Test-Path $targetConfigFile) { Remove-Item $targetConfigFile -Force; $removed += $targetConfigFile }
if ($removed.Count -gt 0) {
    Write-Host "Removed:" -ForegroundColor Yellow
    $removed | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "No current Crafting Expanded files were installed at this target."
}

$backupsRoot = Join-Path $ScriptRoot "test-backups"
$session = Find-LatestMatchingBackup $backupsRoot $TargetRoot
$restored = @()
if ($session) {
    $hadPriorFile = Join-Path $session.FullName "had-prior-install.txt"
    $hadPrior = (Test-Path $hadPriorFile) -and ((Get-Content $hadPriorFile -Raw).Trim() -eq "true")

    if ($hadPrior) {
        $backupPluginDir = Join-Path $session.FullName "plugins\$PluginFolderName"
        $backupConfigFile = Join-Path $session.FullName "config\$ConfigFileName"
        if (Test-Path $backupPluginDir) {
            New-Item -ItemType Directory -Force -Path $targetPluginDir | Out-Null
            Get-ChildItem $backupPluginDir -Recurse -File | ForEach-Object {
                $relative = $_.FullName.Substring($backupPluginDir.Length).TrimStart('\')
                $dest = Join-Path $targetPluginDir $relative
                New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
                Copy-Item $_.FullName $dest -Force
                $restored += $dest
            }
        }
        if (Test-Path $backupConfigFile) {
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetConfigFile) | Out-Null
            Copy-Item $backupConfigFile $targetConfigFile -Force
            $restored += $targetConfigFile
        }
    }

    Set-Content -Path (Join-Path $session.FullName "restored.txt") -Value (Get-Date -Format "o") -Encoding ASCII
    if ($restored.Count -gt 0) {
        Write-Host ""
        Write-Host "Restored previous install from matching backup session: $($session.FullName)" -ForegroundColor Green
        $restored | ForEach-Object { Write-Host "  restored: $_" }
    } else {
        Write-Host ""
        Write-Host "Matching backup session had no prior Crafting Expanded install; target is now clean."
    }
} else {
    Write-Host ""
    Write-Host "No unrestored backup session recorded for this exact target; nothing was restored."
}

Write-Host ""
Write-Host "REMOVE COMPLETE. No other plugin or config file in '$TargetRoot' was touched." -ForegroundColor Green
