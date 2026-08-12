using System;

namespace ErenshorCraftingExpanded
{
    // Plain data describing one verified native craft outcome. Never holds Unity object
    // references (per the user's data-model instruction) so it can flow into progression /
    // commission logic and tests without a live game.
    public sealed class CraftResult
    {
        public readonly string TemplateItemId;
        public readonly string TemplateItemName;
        public readonly string OutputItemId;
        public readonly string OutputItemName;
        public readonly DateTime TimestampUtc;

        public CraftResult(string templateItemId, string templateItemName, string outputItemId, string outputItemName, DateTime timestampUtc)
        {
            TemplateItemId = templateItemId;
            TemplateItemName = templateItemName;
            OutputItemId = outputItemId;
            OutputItemName = outputItemName;
            TimestampUtc = timestampUtc;
        }
    }
}
