$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Find-Csc {
    foreach ($path in @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )) {
        if (Test-Path $path) { return $path }
    }
    throw "csc.exe not found. Install the .NET Framework Developer Pack or Visual Studio Build Tools."
}

$csc = Find-Csc
$out = Join-Path $env:TEMP "ErenshorCraftingExpanded.Tests.exe"

& $csc /nologo /target:exe ("/out:{0}" -f $out) `
    (Join-Path $ScriptRoot "..\src\Persistence\AtomicTextSidecar.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingRecipeInfo.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftableCountPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingProgression.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftSuccessAwardPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeProgressionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingCharacterKey.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingPersistencePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeDiscoveryPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\NativeCraftingEvidencePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CustomRecipePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipePlan.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeRetryPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\NativeRecipeContentPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeDefinitionFactory.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeSelectionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeBinding.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ProductionRecipeRegistrationState.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\CraftingRecipeCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\ExperimentalRecipeDonorPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeOwnershipModels.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\KnownRecipeLedger.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\KnownRecipePersistence.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeTemplateRecoveryPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeTemplateStoragePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeTemplateItemPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Crafting\RecipeBookViewPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Compatibility\RecipeOwnershipIntegration.cs") `
    (Join-Path $ScriptRoot "..\src\Compatibility\SimIdentitySnapshot.cs") `
    (Join-Path $ScriptRoot "..\src\Commissions\CommissionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Commissions\CommissionCadencePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingPanelPositioning.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingPanelLayoutPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingKnowledgePresentationPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\UI\CraftingUiState.cs") `
    (Join-Path $ScriptRoot "..\src\SuiteUiPolicies.cs") `
    (Join-Path $ScriptRoot "..\src\CraftingPointerOwnershipState.cs") `
    (Join-Path $ScriptRoot "..\src\CraftingCameraUiPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeDefinition.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeState.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingInventoryGrantResult.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageGatherCancellationPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageCombatEligibilityPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageActiveGatherClickPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageInteractionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageDepletionLedger.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageAmbiguousGrantQuarantine.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingCharacterKey.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingCharacterReadinessPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageRegionalPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgression.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgressionCodec.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgressionStore.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageBillboardPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagePresentationPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingRuntimeConfigValidation.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagingScanPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForagePlacementPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageEnvironmentPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageResourceCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageResourceSelectionPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageResourceAvailabilityPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ResourceObtainabilityCatalog.cs") `
    (Join-Path $ScriptRoot "..\src\Foraging\ForageVisualPolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\OrganicItemBasePolicy.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CraftingExpandedItemIds.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CustomItemDefinition.cs") `
    (Join-Path $ScriptRoot "..\src\Items\CustomItemRegistry.cs") `
    (Join-Path $ScriptRoot "ForageGatherTransactionTests.cs") `
    (Join-Path $ScriptRoot "RunAllTests.cs")
if ($LASTEXITCODE -ne 0) { throw "Test compilation failed." }

try {
    & $out
    if ($LASTEXITCODE -ne 0) { throw "Erenshor Crafting Expanded tests failed with exit code $LASTEXITCODE." }

    $placementSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageAutoPlacementTrial.cs") -Raw
    if ($placementSource -match '\bother\.ClosestPoint\s*\(') { throw "Foraging regression: Collider.ClosestPoint reintroduced into placement clearance." }

    $clickPatch = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageNativeClickPatch.cs") -Raw
    if ($clickPatch -notmatch 'PlayerControl' -or $clickPatch -notmatch 'LeftClick') { throw "Foraging regression: native LeftClick patch missing." }
    $productionForaging = Get-ChildItem -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging") -Filter '*.cs' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
    if (($productionForaging -join "`n") -match 'GetKey(?:Down|Up)?\s*\([^\)]*(?:KeyCode\.G|ForageKey)') { throw "Foraging regression: keyboard gathering path reintroduced." }

    $registrySource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Crafting\ProductionNativeRecipeRegistry.cs") -Raw
    $actionableAt = $registrySource.IndexOf('ProductionRecipeRetryPolicy.IsActionable', [System.StringComparison]::Ordinal)
    $consumeAt = $registrySource.IndexOf('ProductionRecipeRetryPolicy.ConsumeAttempt', [System.StringComparison]::Ordinal)
    if ($actionableAt -lt 0 -or $consumeAt -lt 0 -or $consumeAt -lt $actionableAt) { throw "Production recipe regression: retry budget can be consumed before live forge evidence." }
    if ($registrySource -notmatch 'ResetAfterGameplayDisable') { throw "Production recipe regression: disable/re-enable retry reset missing." }

    $sidecarSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Persistence\AtomicTextSidecar.cs") -Raw
    if ($sidecarSource -notmatch 'Flush\(true\)' -or $sidecarSource -notmatch '\.tmp' -or $sidecarSource -notmatch '\.bak') { throw "Persistence regression: durable temp/backup recovery primitive incomplete." }

    $controllerSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeController.cs") -Raw
    foreach ($required in @('Available','Gathering','GrantPending','Depleted','gather_begin','gather_cancel','grant_attempt','grant_success','UnknownAfterInvoke','DifferentNodeClick')) {
        if ($controllerSource -notmatch [regex]::Escape($required)) { throw "Foraging gather regression: missing $required transaction contract." }
    }
    if (($controllerSource | Select-String -Pattern 'GrantRegisteredItemForForaging\(' -AllMatches).Matches.Count -ne 1) { throw "Foraging gather regression: custom strict grant call count is not exactly one in controller source." }
    if (($controllerSource | Select-String -Pattern 'GrantVanillaItemForForaging\(' -AllMatches).Matches.Count -ne 1) { throw "Foraging gather regression: vanilla strict grant call count is not exactly one in controller source." }
    if ($controllerSource -match 'GrantRegisteredItem\(_activeRewardItemId' -or $controllerSource -match 'GrantVanillaItem\([^\r\n]*_activeReward') { throw "Foraging gather regression: force-capable generic grant reintroduced." }
    $customGrant = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Compatibility\GameItemRegistryApi.cs") -Raw
    $vanillaGrant = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Compatibility\GameForagingApi.cs") -Raw
    $strictCustomStart = $customGrant.IndexOf('GrantRegisteredItemForForaging', [System.StringComparison]::Ordinal)
    $strictCustomEnd = $customGrant.IndexOf('TryApplyRecipeTemplateSafety', $strictCustomStart, [System.StringComparison]::Ordinal)
    if ($strictCustomStart -lt 0 -or $strictCustomEnd -lt 0 -or $customGrant.Substring($strictCustomStart, $strictCustomEnd-$strictCustomStart) -match 'ForceItemToInv') { throw "Foraging strict custom grant may force inventory insertion." }
    $strictVanillaStart = $vanillaGrant.IndexOf('GrantVanillaItemForForaging', [System.StringComparison]::Ordinal)
    $strictVanillaEnd = $vanillaGrant.IndexOf('TryPlaySuccessfulForageSound', $strictVanillaStart, [System.StringComparison]::Ordinal)
    if ($strictVanillaStart -lt 0 -or $strictVanillaEnd -lt 0 -or $vanillaGrant.Substring($strictVanillaStart, $strictVanillaEnd-$strictVanillaStart) -match 'ForceItemToInv') { throw "Foraging strict vanilla grant may force inventory insertion." }
    if ($vanillaGrant -notmatch 'Misc' -or $vanillaGrant -notmatch 'DropItem' -or $vanillaGrant -notmatch 'PlayerAud\.volume\s*/\s*2f\s*\*\s*GameData\.UIVolume\s*\*\s*GameData\.MasterVol') { throw "Foraging successful-loot sound evidence path missing." }
    if ($vanillaGrant -notmatch 'StartLoot' -or $vanillaGrant -notmatch 'EndLoot') { throw "Foraging optional native animation cleanup adapter incomplete." }

    $settingsSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\CraftingExpandedSettings.cs") -Raw
    if ($settingsSource -notmatch 'GatherDurationSeconds\s*=\s*1\.25f' -or $settingsSource -notmatch 'UseNativeGatherAnimation\s*=\s*false') { throw "Foraging gather production defaults changed unexpectedly." }

    $labelSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForageNodeWorldLabel.cs") -Raw
    if ($labelSource -notmatch 'SetFillFraction\(ForagePresentationPolicy\.ResourceBarFill' -or $labelSource -notmatch 'scale\.x\s*\*=\s*fraction' -or $labelSource -notmatch 'barFill\.pivot\s*=\s*new Vector2\(0f, 0\.5f\)') { throw "Foraging resource-bar gather progress presentation missing." }
    if ($labelSource -match 'new\s+Material\s*\(') { throw "Foraging completion feedback must not allocate/mutate a material." }

    $dragSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\RetainedUiKit.cs") -Raw
    foreach ($required in @('OnPointerDown(', 'CraftingUiPointerOwnership.Acquire', 'Input.GetMouseButton(0)', 'OnApplicationFocus', 'OnApplicationPause', 'forgetwhtuno.erenshor.ui.drag.owners.v1', 'forgetwhtuno.erenshor.ui.drag.nativeBaseline.v1', 'forgetwhtuno.erenshor.ui.drag.nativeBaselineCaptured.v1')) {
        if ($dragSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Crafting retained drag ownership lifecycle/coordination incomplete: $required" }
    }
    $cameraPatch = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\CraftingCameraUiOwnershipPatch.cs") -Raw
    foreach ($required in @('TryVerify', 'UIWindows', 'ModernControls', 'releaseMouse', 'GetAxis', 'DraggingUIElement', 'harmony.Patch', 'CraftingCameraUiPolicy.PromoteUsingUi')) {
        if ($cameraPatch.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Crafting verified CameraController.UsingUI contract incomplete: $required" }
    }
    if ($cameraPatch -match '\[HarmonyPatch\s*\(\s*typeof\(CameraController\)') { throw "Crafting camera containment must not install by unverified attribute target." }

    foreach ($required in @('_activeNativeGrantInvokeStarted', 'out _activeNativeGrantInvokeStarted', 'RecordAmbiguousGrantQuarantine', 'RuntimeExceptionCleanup')) {
        if ($controllerSource.IndexOf($required, [System.StringComparison]::Ordinal) -lt 0) { throw "Foraging post-invoke ambiguity/runtime cleanup contract incomplete: $required" }
    }
    if ($customGrant.IndexOf('out bool nativeInvokeStarted', [System.StringComparison]::Ordinal) -lt 0 -or $vanillaGrant.IndexOf('out bool nativeInvokeStarted', [System.StringComparison]::Ordinal) -lt 0) { throw "Foraging strict adapters must expose native-invoke-started authority." }
    $codecSource = Get-Content -LiteralPath (Join-Path $ScriptRoot "..\src\Foraging\ForagingProgressionCodec.cs") -Raw
    if ($codecSource -notmatch 'quarantine=' -or $codecSource -notmatch 'AmbiguousGrants') { throw "Foraging ambiguous-grant quarantine persistence missing." }

    $allSource = Get-ChildItem -LiteralPath (Join-Path $ScriptRoot "..\src") -Filter '*.cs' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
    if (($allSource -join "`n") -match '\bOnGUI\s*\(') { throw "Retained UI regression: OnGUI found in Crafting source." }

    Write-Host "Source contract checks: PASS" -ForegroundColor Green
}
finally {
    Remove-Item $out -Force -ErrorAction SilentlyContinue
}
