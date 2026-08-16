using System;

namespace ErenshorCraftingExpanded
{
    // Pure retained-panel geometry. Header/footer are stable; variable profession/recipe content
    // lives in one masked body ScrollRect. Desired heights are envelopes, never permission to draw
    // outside the current screen.
    internal static class CraftingPanelLayoutPolicy
    {
        internal const float Width = 440f;
        internal const float CompactHeight = 590f;
        internal const float CommissionHeight = 685f;
        internal const float HeaderHeight = 32f;
        internal const float CollapsedHeight = HeaderHeight;
        internal const float HeaderBodyGap = 10f;
        internal const float OuterInset = 10f;
        internal const float FooterHeight = 116f;
        internal const float ScreenMargin = 16f;
        internal const float MinimumUsableHeight = 280f;
        internal const float BodySpacing = 5f;
        internal const float SectionHeaderHeight = 22f;
        internal const float ProgressLineHeight = 24f;
        internal const float HintLineHeight = 24f;
        internal const float ResourceRowHeight = 42f;
        internal const float MaterialRowHeight = 22f;
        internal const float KnownRecipeRowHeight = 48f;
        internal const float LockedRecipeRowHeight = 44f;

        internal static float HeightFor(bool commissionsEnabled)
        {
            return commissionsEnabled ? CommissionHeight : CompactHeight;
        }

        internal static float HeightFor(bool commissionsEnabled, float screenHeight)
        {
            float desired = HeightFor(commissionsEnabled);
            if (float.IsNaN(screenHeight) || float.IsInfinity(screenHeight) || screenHeight <= 0f) return desired;
            float cap = screenHeight - ScreenMargin;
            if (cap <= 0f) return desired;
            if (cap < MinimumUsableHeight) return Math.Max(180f, cap);
            return Math.Min(desired, cap);
        }

        internal static float HeightForCollapsed()
        {
            return CollapsedHeight;
        }

        internal static float BodyTopInset()
        {
            return HeaderHeight + HeaderBodyGap;
        }

        internal static float BodyBottomInset()
        {
            return OuterInset + FooterHeight + 6f;
        }

        internal static float BodyViewportHeight(float panelHeight)
        {
            float height = panelHeight - BodyTopInset() - BodyBottomInset();
            return height < 0f ? 0f : height;
        }

        internal static bool IsStructurallyContained(float panelHeight)
        {
            if (float.IsNaN(panelHeight) || float.IsInfinity(panelHeight) || panelHeight <= 0f) return false;
            return BodyViewportHeight(panelHeight) >= 0f &&
                BodyTopInset() + BodyViewportHeight(panelHeight) + BodyBottomInset() <= panelHeight + 0.01f;
        }

        internal static float EstimateKnowledgeBodyHeight(
            int resourceRows, int materialRows, int knownRecipeRows, int lockedRecipeRows,
            bool recipeCatalogEmpty, bool commissionsVisible)
        {
            if (resourceRows < 0) resourceRows = 0;
            if (materialRows < 0) materialRows = 0;
            if (knownRecipeRows < 0) knownRecipeRows = 0;
            if (lockedRecipeRows < 0) lockedRecipeRows = 0;

            float height = 0f;
            int topLevelChildren = 0;

            // Professions.
            height += SectionHeaderHeight + (ProgressLineHeight * 2f);
            topLevelChildren += 3;
            // Resource knowledge + next-exploration hint.
            height += SectionHeaderHeight + (ResourceRowHeight * resourceRows) + HintLineHeight;
            topLevelChildren += 3;
            // Current forge knowledge.
            height += SectionHeaderHeight + (ProgressLineHeight * 2f) + (MaterialRowHeight * materialRows);
            topLevelChildren += 4;
            // Recipe summary plus either the compact empty state or normal persistence/rows.
            height += SectionHeaderHeight;
            topLevelChildren += 1;
            if (recipeCatalogEmpty)
            {
                height += 46f;
                topLevelChildren += 1;
            }
            else
            {
                height += ProgressLineHeight + (KnownRecipeRowHeight * knownRecipeRows) + (LockedRecipeRowHeight * lockedRecipeRows);
                topLevelChildren += 2;
            }
            if (commissionsVisible)
            {
                height += 96f;
                topLevelChildren += 1;
            }
            if (topLevelChildren > 1) height += (topLevelChildren - 1) * BodySpacing;
            return height;
        }

        internal static bool RequiresBodyScroll(float panelHeight, float preferredContentHeight)
        {
            if (float.IsNaN(preferredContentHeight) || float.IsInfinity(preferredContentHeight) || preferredContentHeight < 0f) return false;
            return preferredContentHeight > BodyViewportHeight(panelHeight) + 0.01f;
        }

        internal static string BoolButtonText(string label, bool enabled)
        {
            return (label ?? string.Empty) + (enabled ? " [ON]" : " [OFF]");
        }

        internal static string RunSelfTests()
        {
            if (HeightFor(false) != CompactHeight) return "FAIL compact panel envelope";
            if (HeightForCollapsed() != HeaderHeight) return "FAIL collapsed panel should be header-only";
            if (HeightFor(true) != CommissionHeight) return "FAIL commission panel envelope";
            if (CompactHeight >= CommissionHeight) return "FAIL commission mode should have larger envelope";
            if (Width < 400f || Width > 520f) return "FAIL profession panel width bound";

            if (Math.Abs(HeightFor(false, 1080f) - CompactHeight) > 0.01f) return "FAIL 1080p compact height";
            if (Math.Abs(HeightFor(true, 1080f) - CommissionHeight) > 0.01f) return "FAIL 1080p commission height";
            if (Math.Abs(HeightFor(true, 600f) - 584f) > 0.01f) return "FAIL small-screen height cap";
            if (HeightFor(false, 360f) > 344.01f) return "FAIL very-small-screen containment";

            if (!IsStructurallyContained(CompactHeight) || !IsStructurallyContained(CommissionHeight))
                return "FAIL standard panel structural containment";
            if (BodyViewportHeight(CompactHeight) < 400f || BodyViewportHeight(CompactHeight) > 440f)
                return "FAIL compact body viewport height";
            if (BodyViewportHeight(344f) <= 0f) return "FAIL small-screen body should remain scrollable";

            float compactKnowledge = EstimateKnowledgeBodyHeight(1, 0, 0, 0, true, false);
            if (compactKnowledge <= 0f || RequiresBodyScroll(CompactHeight, compactKnowledge)) return "FAIL compact knowledge body should fit standard panel";
            if (!RequiresBodyScroll(344f, compactKnowledge)) return "FAIL compact knowledge body should scroll on small screen";
            float denseKnowledge = EstimateKnowledgeBodyHeight(2, 5, 4, 6, false, true);
            if (!RequiresBodyScroll(CompactHeight, denseKnowledge) || !RequiresBodyScroll(CommissionHeight, denseKnowledge))
                return "FAIL dense knowledge body must use outer scroll";
            if (!IsStructurallyContained(344f)) return "FAIL small-screen outer containment";

            if (BoolButtonText("Crafting", true) != "Crafting [ON]") return "FAIL bool ON label";
            if (BoolButtonText("Foraging", false) != "Foraging [OFF]") return "FAIL bool OFF label";
            return "PASS crafting panel layout policy";
        }
    }
}
