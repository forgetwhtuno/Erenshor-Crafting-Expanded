namespace ErenshorCraftingExpanded
{
    // Gathering-specific inventory authority. This intentionally does not reuse the generic
    // item/template grant booleans because a Foraging node must distinguish a definitive native
    // rejection from an ambiguous exception after AddItemToInv may already have been invoked.
    public enum ForagingInventoryGrantResult
    {
        Success = 0,
        InventoryRejected = 1,
        ItemUnavailable = 2,
        NativeGrantUnavailable = 3,
        UnknownAfterInvoke = 4
    }

    public static class ForagingInventoryGrantPolicy
    {
        public static bool IsSuccess(ForagingInventoryGrantResult result)
        {
            return result == ForagingInventoryGrantResult.Success;
        }

        public static bool RestoresAvailability(ForagingInventoryGrantResult result)
        {
            return result == ForagingInventoryGrantResult.InventoryRejected ||
                result == ForagingInventoryGrantResult.ItemUnavailable ||
                result == ForagingInventoryGrantResult.NativeGrantUnavailable;
        }

        public static bool MustFailClosed(ForagingInventoryGrantResult result)
        {
            return result == ForagingInventoryGrantResult.UnknownAfterInvoke;
        }

        internal static string RunSelfTests()
        {
            if (!IsSuccess(ForagingInventoryGrantResult.Success)) return "FAIL success grant policy";
            if (IsSuccess(ForagingInventoryGrantResult.InventoryRejected)) return "FAIL rejected grant counted as success";
            if (!RestoresAvailability(ForagingInventoryGrantResult.InventoryRejected)) return "FAIL inventory rejection should restore node";
            if (!RestoresAvailability(ForagingInventoryGrantResult.ItemUnavailable)) return "FAIL unavailable item should restore node";
            if (!RestoresAvailability(ForagingInventoryGrantResult.NativeGrantUnavailable)) return "FAIL unavailable native grant should restore node";
            if (RestoresAvailability(ForagingInventoryGrantResult.UnknownAfterInvoke)) return "FAIL ambiguous invoked grant must not reopen node";
            if (!MustFailClosed(ForagingInventoryGrantResult.UnknownAfterInvoke)) return "FAIL ambiguous invoked grant must fail closed";
            if (MustFailClosed(ForagingInventoryGrantResult.InventoryRejected)) return "FAIL definitive rejection should not fail closed";
            return "PASS foraging inventory grant policy";
        }
    }
}
