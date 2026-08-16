namespace ErenshorCraftingExpanded
{
    // Pure lifetime model shared by Crafting's retained drag and resize handlers. Pointer ownership
    // begins on left pointer-down (before Unity's drag threshold), survives drag across canvases,
    // and has one idempotent terminal release path for pointer-up/focus loss/pause/disable/destroy.
    internal sealed class CraftingPointerOwnershipState
    {
        internal bool OwnsPointer { get; private set; }
        internal bool IsDragging { get; private set; }

        internal bool PointerDown(bool leftButton)
        {
            if (!leftButton) return false;
            OwnsPointer = true;
            IsDragging = false;
            return true;
        }

        internal bool BeginDrag()
        {
            if (!OwnsPointer) return false;
            IsDragging = true;
            return true;
        }

        internal bool Release()
        {
            bool owned = OwnsPointer;
            OwnsPointer = false;
            IsDragging = false;
            return owned;
        }

        internal static string RunSelfTests()
        {
            CraftingPointerOwnershipState state = new CraftingPointerOwnershipState();
            if (state.PointerDown(false)) return "FAIL right pointer acquired crafting UI ownership";
            if (state.OwnsPointer) return "FAIL rejected pointer-down left stale ownership";
            if (!state.PointerDown(true) || !state.OwnsPointer || state.IsDragging) return "FAIL left pointer-down ownership";
            if (!state.BeginDrag() || !state.IsDragging) return "FAIL drag did not begin from owned pointer";
            if (!state.Release() || state.OwnsPointer || state.IsDragging) return "FAIL release did not clear pointer/drag state";
            if (state.Release()) return "FAIL repeated release was not idempotent";
            if (state.BeginDrag()) return "FAIL drag began without pointer ownership";
            if (!state.PointerDown(true)) return "FAIL repeated gesture pointer-down";
            if (!state.Release()) return "FAIL repeated gesture release";
            return "PASS crafting pointer ownership state";
        }
    }
}
