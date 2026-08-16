using System;
using ErenshorCraftingExpanded;

internal static class RunAllTests
{
    private static int Main()
    {
        string[] results =
        {
            AtomicTextSidecar.RunSelfTests(),
            CraftableCountPolicy.RunSelfTests(),
            CraftingProgress.RunSelfTests(),
            CraftingProgressionMigrationPolicy.RunSelfTests(),
            CraftSuccessAwardPolicy.RunSelfTests(),
            RecipeProgressionPolicy.RunSelfTests(),
            CraftingCharacterKey.RunSelfTests(),
            CraftingPersistencePolicy.RunSelfTests(),
            ProductionRecipePlan.RunSelfTests(),
            ProductionRecipeRetryPolicy.RunSelfTests(),
            NativeRecipeContentPolicy.RunSelfTests(),
            ProductionRecipeDefinitionFactory.RunSelfTests(),
            ProductionRecipeSelectionPolicy.RunSelfTests(),
            ProductionRecipeBindingCodec.RunSelfTests(),
            ProductionRecipeBindingStore.RunStoreSelfTests(),
            ProductionRecipeRegistrationState.RunSelfTests(),
            CraftingRecipeCatalog.RunSelfTests(),
            ExperimentalRecipeDonorPolicy.RunSelfTests(),
            NativeCraftingEvidencePolicy.RunSelfTests(),
            NativeRecipeMutationGate.RunSelfTests(),

            RecipeOwnershipCatalog.RunSelfTests(),
            KnownRecipeLedger.RunSelfTests(),
            RecipeCharacterIdentityKey.RunSelfTests(),
            KnownRecipeLedgerCodec.RunSelfTests(),
            KnownRecipeStore.RunSelfTests(),
            RecipeTemplateRecoveryPolicy.RunSelfTests(),
            RecipeTemplateStoragePolicy.RunSelfTests(),
            RecipeTemplateItemPolicy.RunSelfTests(),
            RecipeBookViewPolicy.RunSelfTests(),
            RecipeOwnershipIntegration.RunSelfTests(),

            CommissionPolicy.RunSelfTests(),
            CommissionCadencePolicy.RunSelfTests(),
            CraftingPanelPositioning.RunSelfTests(),
            CraftingPanelLayoutPolicy.RunSelfTests(),
            CraftingKnowledgePresentationPolicy.RunSelfTests(),
            CraftingUiStateMachine.RunSelfTests(),
            SuiteUiPositionPolicy.RunSelfTests(),
            CraftingPointerOwnershipState.RunSelfTests(),
                CraftingCameraUiPolicy.RunSelfTests(),

            ForageNodeRuntimeState.RunSelfTests(),
            ForageGatherTransactionTests.Run(),
            ForagingInventoryGrantPolicy.RunSelfTests(),
            ForageGatherCancellationPolicy.RunSelfTests(),
            ForageCombatEligibilityPolicy.RunSelfTests(),
            ForageActiveGatherClickPolicy.RunSelfTests(),
            ForageInteractionPolicy.RunSelfTests(),
            ForageDepletionLedger.RunSelfTests(),
            ForageAmbiguousGrantQuarantine.RunSelfTests(),
            ForagingCharacterKey.RunSelfTests(),
            ForagingCharacterReadinessPolicy.RunSelfTests(),
            ForageRegionalPolicy.RunSelfTests(),
            ForagingProgressionEngine.RunSelfTests(),
            ForagingProgressionCodec.RunSelfTests(),
            ForagingProgressionStore.RunSelfTests(),
            ForageBillboardPolicy.RunSelfTests(),
            ForagePresentationPolicy.RunSelfTests(),
            ForageNodeCatalog.RunSelfTests(),
            ForagingRuntimeConfigValidation.RunSelfTests(),
            ForagingScanPolicy.RunSelfTests(),
            ForagePlacementPolicy.RunSelfTests(),
            ForageEnvironmentPolicy.RunSelfTests(),
            ForageResourceCatalog.RunSelfTests(),
            ForageResourceSelectionPolicy.RunSelfTests(),
            ForageResourceAvailabilityPolicy.RunSelfTests(),
            ResourceObtainabilityCatalog.RunSelfTests(),
            ForageVisualPolicy.RunSelfTests(),

            OrganicItemBasePolicy.RunSelfTests(),
            CustomItemRegistry.RunSelfTests()
        };

        bool allPass = true;
        foreach (string result in results)
        {
            Console.WriteLine(result);
            if (result == null || !result.StartsWith("PASS", StringComparison.Ordinal)) allPass = false;
        }

        Console.WriteLine(allPass ? "RunAllTests: PASS" : "RunAllTests: FAIL");
        return allPass ? 0 : 1;
    }
}
