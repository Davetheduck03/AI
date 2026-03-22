using UnityEngine;

/// <summary>
/// ACTION: Picks up the WorldItem stored on the blackboard as "targetWorldItem".
/// Calls TryEquipWithAssessment directly — no physics trigger required.
/// The world item is always consumed (destroyed) whether or not it was equipped,
/// since the hero has deliberately walked to it and picked it up.
/// Clears both blackboard keys on completion.
/// Returns Success after pickup; Failure if the target item is missing or already gone.
/// </summary>
public class PickupItem : Node
{
    public PickupItem(Blackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        WorldItem worldItem = bb.Get<WorldItem>("targetWorldItem");

        // Item already gone (picked up by physics trigger or destroyed elsewhere)
        if (worldItem == null)
        {
            ClearBlackboard();
            return NodeState.Failure;
        }

        ItemSO item          = worldItem.item;
        Vector3 itemPosition = worldItem.transform.position;

        // ── Relic pickup ──────────────────────────────────────────────────
        if (item is RelicSO relic)
        {
            RelicHolder holder = self.GetComponent<RelicHolder>();
            if (holder != null)
            {
                holder.PickupRelic(relic);
                Debug.Log($"[PickupItem] {self.name} picked up the Relic '{relic.itemName}'");
            }

            if (worldItem != null) Object.Destroy(worldItem.gameObject);
            ClearBlackboard();
            return NodeState.Success;
        }

        // ── Normal equipment pickup ───────────────────────────────────────
        EquipmentComponent equipment = self.GetComponent<EquipmentComponent>();
        if (equipment == null)
        {
            ClearBlackboard();
            return NodeState.Failure;
        }

        bool equipped = equipment.TryEquipWithAssessment(item, itemPosition);

        // Always destroy the world item — the hero has committed to picking it up.
        // If it wasn't equipped (e.g. another hero grabbed a better weapon between
        // EvaluateNearbyItems and this node firing), the item is discarded intentionally.
        if (worldItem != null)
            Object.Destroy(worldItem.gameObject);

        ClearBlackboard();

        Debug.Log($"[PickupItem] {self.name} picked up {item?.itemName} → " +
                  (equipped ? "equipped" : "discarded (no longer an upgrade)"));

        return NodeState.Success;
    }

    private void ClearBlackboard()
    {
        bb.Set<WorldItem>("targetWorldItem", null);
        bb.Set<Transform>("target", null);
    }
}
