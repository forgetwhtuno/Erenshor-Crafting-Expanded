namespace ErenshorCraftingExpanded
{
    public enum ForageActiveGatherClickAction
    {
        IgnoreSameNodeConsume = 0,
        CancelDifferentNodeConsume = 1,
        WorldPassThroughNoCancel = 2,
        CancelTypingPassThrough = 3
    }

    // Pure click policy while one global gather owns the transaction. This makes the anti-chain
    // behavior explicit: a different herb consumes only the cancellation click and never becomes
    // a new gather until the player clicks it again.
    public static class ForageActiveGatherClickPolicy
    {
        public static ForageActiveGatherClickAction Evaluate(bool typing, bool hitForageNode, bool sameNode)
        {
            if (typing) return ForageActiveGatherClickAction.CancelTypingPassThrough;
            if (!hitForageNode) return ForageActiveGatherClickAction.WorldPassThroughNoCancel;
            return sameNode
                ? ForageActiveGatherClickAction.IgnoreSameNodeConsume
                : ForageActiveGatherClickAction.CancelDifferentNodeConsume;
        }

        internal static string RunSelfTests()
        {
            if (Evaluate(false, true, true) != ForageActiveGatherClickAction.IgnoreSameNodeConsume)
                return "FAIL same-node click should be consumed/ignored";
            if (Evaluate(false, true, false) != ForageActiveGatherClickAction.CancelDifferentNodeConsume)
                return "FAIL different-node click should cancel/consume";
            if (Evaluate(false, false, false) != ForageActiveGatherClickAction.WorldPassThroughNoCancel)
                return "FAIL unrelated world click should pass through without changing gather";
            if (Evaluate(true, true, true) != ForageActiveGatherClickAction.CancelTypingPassThrough)
                return "FAIL typing should cancel/pass through before node policy";
            return "PASS active gather click policy";
        }
    }
}
