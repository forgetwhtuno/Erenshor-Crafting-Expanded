using System;
using ErenshorCraftingExpanded;

internal static class RunAllTests
{
    private static int Main()
    {
        string[] results =
        {
            CraftableCountPolicy.RunSelfTests(),
            CraftingProgress.RunSelfTests(),
            CommissionPolicy.RunSelfTests(),
            CraftingPanelPositioning.RunSelfTests(),
            CraftingUiStateMachine.RunSelfTests(),
            SuiteUiPositionPolicy.RunSelfTests(),
            ForageNodeRuntimeState.RunSelfTests(),
            ForageNodeCatalog.RunSelfTests(),
            ForagingRuntimeConfigValidation.RunSelfTests(),
            ForagingScanPolicy.RunSelfTests(),
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
