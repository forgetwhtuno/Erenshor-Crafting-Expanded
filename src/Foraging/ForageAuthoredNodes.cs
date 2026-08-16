namespace ErenshorCraftingExpanded
{
    // Curated production forage content lives here. Keep this list intentionally explicit:
    // one verified scene/position/visual-source entry per node, authored from
    // /craftdiag forage pos + /craftdiag forage scan evidence. Curated entries take precedence;
    // the conservative Wild Herb auto-placement slice is only a fallback for scenes with none.
    internal static class ForageAuthoredNodes
    {
        internal static void RegisterAll(ForageNodeCatalog catalog)
        {
            if (catalog == null) return;

            // Intentionally empty in this snapshot; the first usable Wild Herb slice therefore
            // falls back to conservative runtime auto placement in ordinary gameplay scenes.
            // Paste a complete, survey-verified definition here and register it with:
            // catalog.TryRegister(new ForageNodeDefinition { ... });
            //
            // /craftdiag forage pos now emits a paste-ready skeleton containing Scene, Position,
            // PositionSet=true, and RotationY. Fill VisualSourceScene/HierarchyPath only from
            // /craftdiag forage scan [filter] before registering it.
        }
    }
}
