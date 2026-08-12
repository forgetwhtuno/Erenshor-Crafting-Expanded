using System;

namespace ErenshorCraftingExpanded
{
    public enum CommissionState
    {
        None = 0,
        Offered = 1,
        Accepted = 2,
        Completed = 3,
        Declined = 4,
        Invalidated = 5
    }

    // Plain data. Deliberately holds only a scene-local Sim runtime key (see
    // Compatibility/SimIdentityApi.cs), never a SimPlayer/GameObject reference. The current key
    // is NOT a proven persistent Sim identity, so active PoC commissions are invalidated on zone
    // transitions instead of attempting unsafe rebinding.
    public sealed class CraftingCommission
    {
        public string RequestId;
        public string SimRuntimeKey;
        public string SimName;
        public string RequestedItemId;
        public string RequestedItemName;
        public CommissionState State = CommissionState.None;
        public DateTime OfferedUtc;
        public DateTime? CompletedUtc;
    }
}
