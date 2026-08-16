$ErrorActionPreference = 'Stop'

function Resolve-CraftingGameDir([string]$Explicit) {
    if ($Explicit -and (Test-Path (Join-Path $Explicit 'Erenshor.exe'))) { return (Resolve-Path $Explicit).Path }
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Erenshor' }
    if ($env:ProgramFiles) { $candidates += Join-Path $env:ProgramFiles 'Steam\steamapps\common\Erenshor' }
    foreach ($drive in @('C','D','E','F')) { $candidates += "${drive}:\SteamLibrary\steamapps\common\Erenshor" }

    foreach ($steamRoot in @(
        $(if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'Steam' }),
        $(if ($env:ProgramFiles) { Join-Path $env:ProgramFiles 'Steam' })
    )) {
        if (-not $steamRoot) { continue }
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path $vdf)) { continue }
        [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"') | ForEach-Object {
            $library = $_.Groups[1].Value -replace '\\\\','\'
            $candidates += [IO.Path]::Combine($library, 'steamapps', 'common', 'Erenshor')
        }
    }

    foreach ($candidate in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
        if (Test-Path (Join-Path $candidate 'Erenshor.exe')) { return (Resolve-Path $candidate).Path }
    }
    throw "Erenshor installation not found. Pass -GameDir 'C:\path\to\Erenshor'."
}

function Resolve-CraftingLunarisDir([string]$Explicit, [string]$GameDir, [string]$ScriptRoot) {
    $projectRoot = Split-Path -Parent (Split-Path -Parent $ScriptRoot)
    $candidates = @()
    if ($Explicit) { $candidates += $Explicit }
    # Prefer references that physically belong to the target install/profile. Lunaris layouts have
    # changed over time, so probe a few conservative target-relative folders before developer refs.
    $candidates += $GameDir
    $candidates += (Join-Path $GameDir 'plugins')
    $candidates += (Join-Path $GameDir 'Lunaris')
    $candidates += (Join-Path $GameDir 'Lunaris\lib')
    $candidates += (Join-Path $GameDir 'Lunaris\libs')
    $candidates += (Join-Path $GameDir 'plugins\Lunaris')
    $candidates += (Join-Path $GameDir 'plugins\Lunaris\lib')
    $candidates += (Join-Path $GameDir 'plugins\Lunaris\libs')
    $candidates += (Join-Path $GameDir 'LunarisLibs')
    $candidates += (Join-Path $ScriptRoot 'LunarisLibs')
    $candidates += (Join-Path $projectRoot 'reference-assemblies')
    $candidates += (Join-Path $projectRoot 'mods\DeepSim-erenshor\LunarisLibs')

    $resolvedGamePrefix = ((Resolve-Path $GameDir).Path.TrimEnd('\') + '\')
    foreach ($candidate in ($candidates | Where-Object { $_ } | Select-Object -Unique)) {
        if ((Test-Path (Join-Path $candidate 'Lunaris.dll')) -and (Test-Path (Join-Path $candidate '0Harmony.dll'))) {
            $resolved = (Resolve-Path $candidate).Path
            if (-not ($resolved + '\').StartsWith($resolvedGamePrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                Write-Warning "Using Lunaris developer references outside the target Erenshor install: $resolved. Verify the printed Lunaris SHA-256 matches the runtime/profile you intend to test, or pass -LunarisLibDir explicitly."
            }
            return $resolved
        }
    }
    throw "Could not find Lunaris.dll + 0Harmony.dll. Pass -LunarisLibDir pointing at the current target profile references."
}

function Resolve-CraftingCsc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw 'csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools.'
}

function Assert-CraftingReferences([string[]]$References) {
    foreach ($ref in $References) {
        if (-not (Test-Path $ref)) { throw "Missing reference: $ref" }
    }
}

function Get-CraftingSha256([string]$Path) {
    if (-not (Test-Path $Path)) { return '' }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Install-CraftingDllVerified([string]$BuiltDll, [string]$GameDir, [string]$BackupRoot) {
    if (-not (Test-Path $BuiltDll)) { throw "Built DLL missing: $BuiltDll" }
    $pluginsDir = Join-Path $GameDir 'plugins'
    New-Item -ItemType Directory -Force -Path $pluginsDir | Out-Null
    $destination = Join-Path $pluginsDir 'ErenshorCraftingExpanded.dll'
    $builtHash = Get-CraftingSha256 $BuiltDll
    if (-not $builtHash) { throw 'Could not hash built Crafting DLL.' }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    if (-not $BackupRoot) { $BackupRoot = Join-Path (Split-Path -Parent $BuiltDll) '..\test-backups' }
    $session = Join-Path $BackupRoot $timestamp
    New-Item -ItemType Directory -Force -Path $session | Out-Null
    Set-Content -LiteralPath (Join-Path $session 'target-root.txt') -Value $GameDir -Encoding UTF8

    $hadPrior = Test-Path $destination
    Set-Content -LiteralPath (Join-Path $session 'had-prior-install.txt') -Value $hadPrior.ToString() -Encoding UTF8
    $backup = ''
    if ($hadPrior) {
        $backup = Join-Path $session 'ErenshorCraftingExpanded.dll.bak'
        Copy-Item -LiteralPath $destination -Destination $backup -Force
        $priorHash = Get-CraftingSha256 $destination
        $backupHash = Get-CraftingSha256 $backup
        if ($priorHash -ne $backupHash) { throw 'Existing Crafting DLL backup hash verification failed; install aborted.' }
        Set-Content -LiteralPath (Join-Path $session 'prior-sha256.txt') -Value $priorHash -Encoding ASCII
    }

    try {
        Copy-Item -LiteralPath $BuiltDll -Destination $destination -Force
        $installedHash = Get-CraftingSha256 $destination
        if ($installedHash -ne $builtHash) { throw 'Installed Crafting DLL hash does not match built DLL.' }
        Set-Content -LiteralPath (Join-Path $session 'installed-sha256.txt') -Value $installedHash -Encoding ASCII
    }
    catch {
        if ($hadPrior -and $backup -and (Test-Path $backup)) {
            Copy-Item -LiteralPath $backup -Destination $destination -Force
            $rollbackHash = Get-CraftingSha256 $destination
            if ($rollbackHash -ne $priorHash) { throw "Install failed and rollback hash verification also failed (expected=$priorHash actual=$rollbackHash)." }
        } elseif (Test-Path $destination) {
            Remove-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue
        }
        throw
    }

    return [PSCustomObject]@{
        Destination = $destination
        Hash = $builtHash
        Backup = $backup
        Session = $session
    }
}
