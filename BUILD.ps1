param([string]$GameDir = "", [string]$LunarisLibDir = "")

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptRoot 'BuildSupport.ps1')

$GameDir = Resolve-CraftingGameDir $GameDir
$LunarisLibDir = Resolve-CraftingLunarisDir $LunarisLibDir $GameDir $ScriptRoot
$csc = Resolve-CraftingCsc
$managed = Join-Path $GameDir 'Erenshor_Data\Managed'
$outDir = Join-Path $ScriptRoot 'bin'; New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'ErenshorCraftingExpanded.dll'
$refs = @(
    (Join-Path $LunarisLibDir 'Lunaris.dll'), (Join-Path $LunarisLibDir '0Harmony.dll'), (Join-Path $managed 'Assembly-CSharp.dll'), (Join-Path $managed 'netstandard.dll'),
    (Join-Path $managed 'UnityEngine.dll'), (Join-Path $managed 'UnityEngine.CoreModule.dll'), (Join-Path $managed 'UnityEngine.UIModule.dll'),
    (Join-Path $managed 'UnityEngine.AIModule.dll'), (Join-Path $managed 'UnityEngine.AnimationModule.dll'), (Join-Path $managed 'UnityEngine.AudioModule.dll'), (Join-Path $managed 'UnityEngine.PhysicsModule.dll'),
    (Join-Path $managed 'UnityEngine.TerrainPhysicsModule.dll'), (Join-Path $managed 'UnityEngine.UI.dll'), (Join-Path $managed 'Unity.TextMeshPro.dll'),
    (Join-Path $managed 'UnityEngine.TextRenderingModule.dll'), (Join-Path $managed 'UnityEngine.InputLegacyModule.dll'), (Join-Path $managed 'UnityEngine.JSONSerializeModule.dll')
)
Assert-CraftingReferences $refs
$sources = Get-ChildItem -LiteralPath (Join-Path $ScriptRoot 'src') -Filter '*.cs' -Recurse | Sort-Object FullName
if ($sources.Count -eq 0) { throw 'No source files found under src.' }
$rsp = Join-Path $env:TEMP ('ErenshorCraftingExpanded-' + [Guid]::NewGuid().ToString('N') + '.rsp')
try {
    $lines = @('/nologo', '/target:library', '/optimize+', ('/out:"{0}"' -f $out))
    $refs | ForEach-Object { $lines += ('/reference:"{0}"' -f $_) }
    $sources | ForEach-Object { $lines += ('"' + $_.FullName + '"') }
    $lines | Set-Content -LiteralPath $rsp -Encoding ASCII
    Write-Host "Building current local Crafting Expanded source against $managed" -ForegroundColor Cyan
    Write-Host "Lunaris references: $LunarisLibDir" -ForegroundColor Cyan
    Write-Host "Assembly-CSharp SHA-256: $(Get-CraftingSha256 (Join-Path $managed 'Assembly-CSharp.dll'))" -ForegroundColor Cyan
    Write-Host "Lunaris SHA-256: $(Get-CraftingSha256 (Join-Path $LunarisLibDir 'Lunaris.dll'))" -ForegroundColor Cyan
    & $csc "@$rsp"
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed (csc exit code $LASTEXITCODE)." }
} finally { Remove-Item -LiteralPath $rsp -Force -ErrorAction SilentlyContinue }
Write-Host "BUILD OK: $out" -ForegroundColor Green
Write-Host "DLL SHA-256: $(Get-CraftingSha256 $out)" -ForegroundColor Green
