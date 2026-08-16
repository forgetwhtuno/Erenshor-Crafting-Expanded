using ErenshorCraftingExpanded;

internal static class ForageGatherTransactionTests
{
    private sealed class Model
    {
        internal readonly ForageNodeRuntimeState Node = new ForageNodeRuntimeState();
        internal int GrantCalls;
        internal int XpCommits;
        internal int DiscoveryCommits;
        internal int SuccessLedgerCommits;
        internal bool DiscoveryKnown;

        internal bool Begin(long token) { return Node.TryBeginGather(token, 1.25f); }
        internal void ReachGrant(long token)
        {
            Node.Tick(1.25f);
            if (!Node.TryEnterGrantPending(token)) throw new System.Exception("grant pending transition failed");
        }

        internal void Complete(long token, ForagingInventoryGrantResult result, bool presentationThrows)
        {
            if (Node.Availability != ForageAvailability.GrantPending || !Node.IsTokenActive(token)) return;
            GrantCalls++;
            if (result == ForagingInventoryGrantResult.Success)
            {
                if (!Node.CompleteGrantSuccess(token, 300f)) throw new System.Exception("success transition failed");
                SuccessLedgerCommits++;
                XpCommits++;
                if (!DiscoveryKnown) { DiscoveryKnown = true; DiscoveryCommits++; }
                if (presentationThrows) throw new System.Exception("simulated presentation failure");
                return;
            }
            if (ForagingInventoryGrantPolicy.RestoresAvailability(result))
            {
                Node.RejectGrant(token);
                return;
            }
            Node.FailClosedUnknownAfterInvoke(token, 300f);
        }
    }

    internal static string Run()
    {
        Model doubleClick = new Model();
        if (!doubleClick.Begin(1)) return "FAIL transaction first click";
        if (doubleClick.Begin(2)) return "FAIL transaction same-frame double click restarted";
        doubleClick.ReachGrant(1);
        doubleClick.Complete(1, ForagingInventoryGrantResult.Success, false);
        doubleClick.Complete(1, ForagingInventoryGrantResult.Success, false);
        if (doubleClick.GrantCalls != 1 || doubleClick.XpCommits != 1 || doubleClick.DiscoveryCommits != 1 || doubleClick.SuccessLedgerCommits != 1)
            return "FAIL successful gather was not exactly once";

        Model rejected = new Model();
        rejected.Begin(10); rejected.ReachGrant(10); rejected.Complete(10, ForagingInventoryGrantResult.InventoryRejected, false);
        if (rejected.Node.Availability != ForageAvailability.Available || rejected.XpCommits != 0 || rejected.SuccessLedgerCommits != 0)
            return "FAIL inventory rejection rollback authority";

        Model unavailable = new Model();
        unavailable.Begin(11); unavailable.ReachGrant(11); unavailable.Complete(11, ForagingInventoryGrantResult.ItemUnavailable, false);
        if (unavailable.Node.Availability != ForageAvailability.Available || unavailable.GrantCalls != 1 || unavailable.XpCommits != 0)
            return "FAIL item unavailable rollback authority";

        Model nativeUnavailable = new Model();
        nativeUnavailable.Begin(111); nativeUnavailable.ReachGrant(111); nativeUnavailable.Complete(111, ForagingInventoryGrantResult.NativeGrantUnavailable, false);
        if (nativeUnavailable.Node.Availability != ForageAvailability.Available || nativeUnavailable.GrantCalls != 1 || nativeUnavailable.XpCommits != 0 || nativeUnavailable.SuccessLedgerCommits != 0)
            return "FAIL native grant unavailable rollback authority";

        Model unknown = new Model();
        unknown.Begin(12); unknown.ReachGrant(12); unknown.Complete(12, ForagingInventoryGrantResult.UnknownAfterInvoke, false);
        if (unknown.Node.Availability != ForageAvailability.Depleted || unknown.XpCommits != 0 || unknown.SuccessLedgerCommits != 0 || unknown.Begin(13))
            return "FAIL unknown-after-invoke fail-closed authority";

        Model presentation = new Model();
        presentation.Begin(20); presentation.ReachGrant(20);
        try { presentation.Complete(20, ForagingInventoryGrantResult.Success, true); } catch { }
        presentation.Complete(20, ForagingInventoryGrantResult.Success, false);
        if (presentation.Node.Availability != ForageAvailability.Depleted || presentation.GrantCalls != 1 || presentation.XpCommits != 1 || presentation.SuccessLedgerCommits != 1)
            return "FAIL presentation failure reopened successful transaction";

        return "PASS forage gather transaction authority";
    }
}
