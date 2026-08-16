namespace ErenshorCraftingExpanded
{
    public sealed class NativeRecipeOutputFacts
    {
        public bool ShapeKnown;
        public bool RequiredSlotGeneral;
        public bool Stackable;
        public bool Unique;
        public bool Rare;
        public bool Template;
        public bool FuelSource;
        public int ItemValue;
        public bool Disposable;
        public bool MustBeEquippedToClick;
        public bool HasClickEffect;
        public bool HasTeachSpell;
        public bool HasTeachSkill;
        public bool HasQuestReadBehavior;
        public bool HasAura;
        public bool HasWornEffect;
        public bool HasWeaponProc;
        public bool OwnedByMod;
    }

    public static class NativeRecipeContentPolicy
    {
        public static bool IsCommonSafeOutput(NativeRecipeOutputFacts facts)
        {
            return facts != null && facts.ShapeKnown && facts.RequiredSlotGeneral && facts.Stackable && !facts.Unique && !facts.Rare &&
                !facts.Template && !facts.FuelSource && facts.ItemValue > 0 && !facts.MustBeEquippedToClick &&
                !facts.HasTeachSpell && !facts.HasTeachSkill && !facts.HasQuestReadBehavior && !facts.HasAura &&
                !facts.HasWornEffect && !facts.HasWeaponProc && !facts.OwnedByMod;
        }

        public static bool Matches(ProductionRecipeContentKind kind, NativeRecipeOutputFacts facts)
        {
            if (!IsCommonSafeOutput(facts)) return false;
            if (kind == ProductionRecipeContentKind.ActivatedUtility)
                return facts.HasClickEffect && facts.Disposable;
            return !facts.HasClickEffect && !facts.Disposable;
        }

        public static bool FitsForge(int donorDistinctIngredients, int componentSlots, ProductionRecipeContentKind kind)
        {
            if (donorDistinctIngredients <= 0 || componentSlots <= 0) return false;
            int required = donorDistinctIngredients + (kind == ProductionRecipeContentKind.ActivatedUtility ? 1 : 0);
            return required <= componentSlots;
        }

        internal static string RunSelfTests()
        {
            NativeRecipeOutputFacts utility = SafeBase();
            utility.HasClickEffect = true;
            utility.Disposable = true;
            if (!Matches(ProductionRecipeContentKind.ActivatedUtility, utility)) return "FAIL activated utility accepted";
            if (Matches(ProductionRecipeContentKind.Foundation, utility)) return "FAIL activated utility misclassified";

            NativeRecipeOutputFacts foundation = SafeBase();
            if (!Matches(ProductionRecipeContentKind.Foundation, foundation)) return "FAIL foundation accepted";
            foundation.Rare = true;
            if (Matches(ProductionRecipeContentKind.Foundation, foundation)) return "FAIL rare output accepted";
            foundation = SafeBase();
            foundation.HasQuestReadBehavior = true;
            if (Matches(ProductionRecipeContentKind.Foundation, foundation)) return "FAIL quest output accepted";
            utility = SafeBase(); utility.HasClickEffect = true; utility.Disposable = true; utility.MustBeEquippedToClick = true;
            if (Matches(ProductionRecipeContentKind.ActivatedUtility, utility)) return "FAIL equipped click output accepted";
            if (!FitsForge(3, 4, ProductionRecipeContentKind.ActivatedUtility) || FitsForge(4, 4, ProductionRecipeContentKind.ActivatedUtility)) return "FAIL herb forge capacity";
            if (!FitsForge(4, 4, ProductionRecipeContentKind.Foundation)) return "FAIL foundation forge capacity";
            return "PASS native recipe content policy";
        }

        private static NativeRecipeOutputFacts SafeBase()
        {
            NativeRecipeOutputFacts facts = new NativeRecipeOutputFacts();
            facts.ShapeKnown = true;
            facts.RequiredSlotGeneral = true;
            facts.Stackable = true;
            facts.ItemValue = 1;
            return facts;
        }
    }
}
