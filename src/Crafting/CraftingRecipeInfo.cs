using System.Collections.Generic;

namespace ErenshorCraftingExpanded
{
    // A required ingredient line, expanded from Item.TemplateIngredients' repeated-entry
    // encoding (see docs/NATIVE_CRAFTING_FINDINGS.md section 2) into an explicit count.
    public struct RequirementLine
    {
        public readonly string ItemId;
        public readonly string ItemName;
        public readonly int Quantity;

        public RequirementLine(string itemId, string itemName, int quantity)
        {
            ItemId = itemId;
            ItemName = itemName;
            Quantity = quantity;
        }
    }

    // Plain-data snapshot of the recipe currently loaded into the forge's Template slot.
    // Never holds a Unity object reference so it can be passed into pure policy code and tests.
    public sealed class CraftRecipeSnapshot
    {
        public string TemplateItemId;
        public string TemplateItemName;
        public string OutputItemId;
        public string OutputItemName;
        public List<RequirementLine> Requirements = new List<RequirementLine>();
    }

    // Read-only view of what the player currently has available for a given item id, used by
    // CraftableCountPolicy. Kept separate from live ItemIcon/Inventory objects so the count
    // math is testable without the game.
    public struct InventoryAvailability
    {
        public readonly string ItemId;
        public readonly int Quantity;

        public InventoryAvailability(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }
}
