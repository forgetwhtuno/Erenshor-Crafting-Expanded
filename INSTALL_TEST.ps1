<#
.SYNOPSIS
  Reversible test install of Erenshor Crafting Expanded into one explicit BepInEx profile.

.DESCRIPTION
  Resolves the install target first, builds against THAT profile's BepInEx/Harmony references
  (unless -SkipBuild), then copies ONLY this mod's own plugin folder/config into the target.
  Every install creates a timestamped backup-session record under test-backups\, even if there
  was no prior Crafting Expanded install. REMOVE_TEST.ps1 uses the recorded target path and
  session state so it never restores a backup from a different profile. Never launches the game.

.PARAMETER GameDir
  Erenshor install directory, used only when a build is performed.

.PARAMETER BepInExRoot
  Target profile root containing BepInEx\. Required when more than one profile exists.

.PARAMETER SkipBuild
  Use the existing bin\ErenshorCraftingExpanded.dll. Intended for disposable/fake-profile script
  validation where the target does not contain real BepInEx core reference assemblies.
#>
param(
    [string]$GameDir = "",
    [string]$BepInExRoot = "",
    [switch]$SkipBuild
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

Write-Host "=== Erenshor Crafting Expanded - TEST install (reversible) ===" -ForegroundColor Cyan

# Resolve the exact install target BEFORE compiling so BUILD.ps1 cannot quietly use references
# from a different profile than the one receiving the DLL.
$roots = @(Find-Roots $BepInExRoot)
if ($roots.Count -eq 0) {
    throw "No BepInEx profile found under r2modman. Pass -BepInExRoot explicitly."
}
if ($roots.Count -gt 1 -and -not $BepInExRoot) {
    Write-Host "Multiple BepInEx profiles found - refusing to guess. Re-run with -BepInExRoot pointing at exactly one:" -ForegroundColor Red
    $roots | ForEach-Object { Write-Host "  $_" }
    throw "Ambiguous install target."
}
$TargetRoot = if ($BepInExRoot) { (Resolve-Path $BepInExRoot).Path } else { $roots[0] }

$builtDll = Join-Path $ScriptRoot "bin\ErenshorCraftingExpanded.dll"
if (-not $SkipBuild) {
    $targetBepInExDll = Join-Path $TargetRoot "BepInEx\core\BepInEx.dll"
    if (-not (Test-Path $targetBepInExDll)) {
        throw "Target '$TargetRoot' has no BepInEx\\core\\BepInEx.dll. Use a real profile for build+install, or -SkipBuild only for disposable script validation."
    }
    Write-Host "Building against the selected target profile first..."
    $buildArgs = @("-BepInExRoot", $TargetRoot)
    if ($GameDir) { $buildArgs += @("-GameDir", $GameDir) }
    & (Join-Path $ScriptRoot "BUILD.ps1") @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed - refusing to install." }
}
if (-not (Test-Path $builtDll)) {
    throw "Built DLL not found at '$builtDll'. Run BUILD.ps1 first, or omit -SkipBuild."
}

$targetPluginDir = Join-Path $TargetRoot "BepInEx\plugins\$PluginFolderName"
$targetConfigDir = Join-Path $TargetRoot "BepInEx\config"
$targetConfigFile = Join-Path $targetConfigDir $ConfigFileName

Write-Host "Install target: $TargetRoot"
Write-Host "Plugin folder:  $targetPluginDir"
Write-Host "Config file:    $targetConfigFile"

# --- Create a target-bound backup session before touching the install ---
$timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss_fff"
$backupRoot = Join-Path $ScriptRoot "test-backups\$timestamp"
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
Set-Content -Path (Join-Path $backupRoot "target-root.txt") -Value $TargetRoot -Encoding UTF8

$backedUp = @()
if (Test-Path $targetPluginDir) {
    $backupPluginDir = Join-Path $backupRoot "plugins\$PluginFolderName"
    New-Item -ItemType Directory -Force -Path $backupPluginDir | Out-Null
    Get-ChildItem $targetPluginDir -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($targetPluginDir.Length).TrimStart('\')
        $dest = Join-Path $backupPluginDir $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
        Copy-Item $_.FullName $dest -Force
        $backedUp += $dest
    }
}
if (Test-Path $targetConfigFile) {
    $backupConfigDir = Join-Path $backupRoot "config"
    New-Item -ItemType Directory -Force -Path $backupConfigDir | Out-Null
    $dest = Join-Path $backupConfigDir $ConfigFileName
    Copy-Item $targetConfigFile $dest -Force
    $backedUp += $dest
}
$hadPriorInstall = $backedUp.Count -gt 0
Set-Content -Path (Join-Path $backupRoot "had-prior-install.txt") -Value ($hadPriorInstall.ToString().ToLowerInvariant()) -Encoding ASCII

if ($hadPriorInstall) {
    Write-Host ""
    Write-Host "Backed up existing Crafting Expanded install to: $backupRoot" -ForegroundColor Yellow
    $backedUp | ForEach-Object { Write-Host "  backup: $_" }
} else {
    Write-Host ""
    Write-Host "No existing Crafting Expanded install found; recorded an empty restore point at: $backupRoot"
}

# --- Copy only this mod's own files ---
New-Item -ItemType Directory -Force -Path $targetPluginDir | Out-Null
$destDll = Join-Path $targetPluginDir "ErenshorCraftingExpanded.dll"
Copy-Item $builtDll $destDll -Force

Write-Host ""
Write-Host "Copied:" -ForegroundColor Green
Write-Host "  $destDll"
Write-Host ""
Write-Host "INSTALL TEST COMPLETE. Nothing else in '$TargetRoot' was touched." -ForegroundColor Green
Write-Host "Backup session: $backupRoot"
Write-Host "The game was NOT launched. Start it manually and follow docs/FIRST_RUNTIME_TEST.md."
Write-Host "To undo this exact test install, run REMOVE_TEST.ps1 with the same -BepInExRoot."
