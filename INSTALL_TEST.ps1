<#
.SYNOPSIS
  Reversible, hash-verified test install for Erenshor Crafting Expanded.

.DESCRIPTION
  Resolves the exact target game first, builds against that install's current Assembly-CSharp and
  Unity assemblies, verifies the existing DLL backup byte-for-byte, installs the new DLL, and
  verifies the installed SHA-256 equals the build output. REMOVE_TEST.ps1 restores the session.
#>
param(
    [string]$GameDir = "",
    [string]$LunarisLibDir = ""
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptRoot 'BuildSupport.ps1')

Write-Host '=== Erenshor Crafting Expanded - reversible TEST install ===' -ForegroundColor Cyan
$GameDir = Resolve-CraftingGameDir $GameDir
Write-Host "Target Erenshor install: $GameDir"

& (Join-Path $ScriptRoot 'BUILD.ps1') -GameDir $GameDir -LunarisLibDir $LunarisLibDir
if ($LASTEXITCODE -ne 0) { throw 'Build failed; nothing installed, nothing backed up.' }

$builtDll = Join-Path $ScriptRoot 'bin\ErenshorCraftingExpanded.dll'
$installed = Install-CraftingDllVerified -BuiltDll $builtDll -GameDir $GameDir -BackupRoot (Join-Path $ScriptRoot 'test-backups')

Write-Host ''
Write-Host 'TEST INSTALL OK' -ForegroundColor Green
Write-Host "  Installed: $($installed.Destination)"
Write-Host "  SHA-256: $($installed.Hash)"
Write-Host "  Backup session: $($installed.Session)"
Write-Host ''
Write-Host 'Run REMOVE_TEST.ps1 (same -GameDir) when testing is complete.'
