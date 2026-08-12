namespace ErenshorCraftingExpanded
{
    // Hidden: no current crafting context. Available: contextual toggle visible. Open: panel open
    // only while context remains relevant. PinnedOpen: explicit user choice to keep the panel
    // available even after the forge/request context goes away.
    public enum CraftingUiState
    {
        Hidden = 0,
        Available = 1,
        Open = 2,
        PinnedOpen = 3
    }

    internal static class CraftingUiStateMachine
    {
        internal static CraftingUiState Current = CraftingUiState.Hidden;

        internal static void OnContextRelevant(bool relevant)
        {
            if (relevant)
            {
                if (Current == CraftingUiState.Hidden) Current = CraftingUiState.Available;
            }
            else
            {
                if (Current == CraftingUiState.Available || Current == CraftingUiState.Open)
                    Current = CraftingUiState.Hidden;
                // PinnedOpen intentionally survives loss of context.
            }
        }

        internal static void ToggleOpen()
        {
            if (Current == CraftingUiState.Open || Current == CraftingUiState.PinnedOpen)
                Current = CraftingUiState.Available;
            else if (Current == CraftingUiState.Available || Current == CraftingUiState.Hidden)
                Current = CraftingUiState.Open;
        }

        internal static void SetPinned(bool pinned)
        {
            if (pinned && Current == CraftingUiState.Open) Current = CraftingUiState.PinnedOpen;
            else if (!pinned && Current == CraftingUiState.PinnedOpen) Current = CraftingUiState.Open;
        }

        internal static bool IsPanelVisible()
        {
            return Current == CraftingUiState.Open || Current == CraftingUiState.PinnedOpen;
        }

        internal static bool IsToggleVisible()
        {
            return Current != CraftingUiState.Hidden;
        }

        internal static string RunSelfTests()
        {
            Current = CraftingUiState.Hidden;
            OnContextRelevant(true);
            if (Current != CraftingUiState.Available) return "FAIL relevant context should expose toggle";

            ToggleOpen();
            if (Current != CraftingUiState.Open) return "FAIL toggle should open panel";
            OnContextRelevant(false);
            if (Current != CraftingUiState.Hidden) return "FAIL unpinned open panel should close when context ends";

            OnContextRelevant(true);
            ToggleOpen();
            SetPinned(true);
            if (Current != CraftingUiState.PinnedOpen) return "FAIL pin should enter PinnedOpen";
            OnContextRelevant(false);
            if (Current != CraftingUiState.PinnedOpen) return "FAIL pinned panel should survive context ending";
            SetPinned(false);
            if (Current != CraftingUiState.Open) return "FAIL unpin should return to Open";
            OnContextRelevant(false);
            if (Current != CraftingUiState.Hidden) return "FAIL unpinned panel should then hide without context";

            Current = CraftingUiState.Hidden;
            return "PASS crafting ui state";
        }
    }
}
