<#
.SYNOPSIS
  One-shot build + install for Erenshor Crafting Expanded. NO backup is taken.

.DESCRIPTION
  Convenience wrapper for fast local iteration. Builds via BUILD.ps1, then copies the resulting
  DLL straight into <GameDir>\plugins\ErenshorCraftingExpanded.dll, overwriting whatever is
  already there. If you want a reversible test install with an automatic backup/restore session,
  use INSTALL_TEST.ps1 / REMOVE_TEST.ps1 instead.

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

Write-Host "=== Erenshor Crafting Expanded - build and install (no backup) ===" -ForegroundColor Cyan
Write-Host "This overwrites any existing plugins\ErenshorCraftingExpanded.dll with no backup." -ForegroundColor Yellow
Write-Host "Use INSTALL_TEST.ps1 instead if you want a reversible, backed-up install." -ForegroundColor Yellow

& (Join-Path $ScriptRoot "BUILD.ps1") -GameDir $GameDir -LunarisLibDir $LunarisLibDir
if ($LASTEXITCODE -ne 0) { throw "Build failed; nothing installed." }

# Re-resolve GameDir the same way BUILD.ps1 did, since BUILD.ps1 runs in its own scope.
if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir "Erenshor.exe"))) {
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} "Steam\steamapps\common\Erenshor" }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles "Steam\steamapps\common\Erenshor" }
    foreach ($drive in @("C","D","E","F")) { $candidates += "${drive}:\SteamLibrary\steamapps\common\Erenshor" }
    $GameDir = $null
    foreach ($candidate in ($candidates | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate "Erenshor.exe")) { $GameDir = (Resolve-Path $candidate).Path; break }
    }
    if (-not $GameDir) { throw "Erenshor installation not found. Pass -GameDir 'C:\path\to\Erenshor'." }
}

$builtDll = Join-Path $ScriptRoot "bin\ErenshorCraftingExpanded.dll"
if (-not (Test-Path $builtDll)) { throw "Expected build output not found: $builtDll" }

$pluginsDir = Join-Path $GameDir "plugins"
New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
$installedDll = Join-Path $pluginsDir "ErenshorCraftingExpanded.dll"
Copy-Item $builtDll $installedDll -Force

Write-Host ""
Write-Host "INSTALLED (no backup taken)" -ForegroundColor Green
Write-Host "  $installedDll"
Write-Host "Restart Erenshor (or use Lunaris's reload if available) to pick up the change."
