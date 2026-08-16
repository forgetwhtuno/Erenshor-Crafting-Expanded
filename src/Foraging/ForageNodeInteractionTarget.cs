using UnityEngine;

namespace ErenshorCraftingExpanded
{
    // Mod-owned, non-blocking raycast target. It exists only to make the visible resource an
    // actual interaction object; no native MiningNode/combat/resource components are borrowed.
    internal sealed class ForageNodeInteractionTarget : MonoBehaviour
    {
        internal SpawnedForageNode Node;
        internal SphereCollider HitCollider;

        internal void SetAvailable(bool available)
        {
            if (HitCollider != null && HitCollider.enabled != available) HitCollider.enabled = available;
        }

        private void OnDestroy()
        {
            Node = null;
            HitCollider = null;
        }
    }
}
