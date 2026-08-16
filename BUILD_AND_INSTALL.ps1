param([string]$GameDir = "", [string]$LunarisLibDir = "")

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptRoot 'BuildSupport.ps1')

Write-Host 'LOCAL DIRTY SOURCE BUILD - Crafting / Foraging' -ForegroundColor Yellow
$GameDir = Resolve-CraftingGameDir $GameDir
& (Join-Path $ScriptRoot 'tests\RUN_TESTS.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Deterministic tests failed; nothing installed.' }
& (Join-Path $ScriptRoot 'BUILD.ps1') -GameDir $GameDir -LunarisLibDir $LunarisLibDir
if ($LASTEXITCODE -ne 0) { throw 'Build failed; nothing installed.' }
$builtDll = Join-Path $ScriptRoot 'bin\ErenshorCraftingExpanded.dll'
$installed = Install-CraftingDllVerified -BuiltDll $builtDll -GameDir $GameDir -BackupRoot (Join-Path $ScriptRoot 'install-backups')
$resultFile = Join-Path $ScriptRoot 'LOCAL_BUILD_RESULT.txt'
@("version=0.2.3", "gameDir=$GameDir", "installed=$($installed.Destination)", "sha256=$($installed.Hash)", "backup=$($installed.Backup)", "completedUtc=$([DateTime]::UtcNow.ToString('o'))") | Set-Content -LiteralPath $resultFile -Encoding UTF8
Write-Host '============================================================' -ForegroundColor Green
Write-Host 'CRAFTING / FORAGING BUILD AND INSTALL COMPLETED SUCCESSFULLY' -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor Green
Write-Host "Built DLL: $builtDll"
Write-Host "Installed DLL: $($installed.Destination)"
Write-Host "SHA-256: $($installed.Hash)"
Write-Host "Backup: $($installed.Backup)"
Write-Host "Result: $resultFile"
