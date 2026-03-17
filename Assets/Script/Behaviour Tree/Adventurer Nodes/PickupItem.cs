using UnityEngine;

/// <summary>
/// ACTION: Picks up the WorldItem stored on the blackboard as "targetWorldItem".
/// Calls TryEquipWithAssessment directly — no physics trigger required.
/// Clears both blackboard keys on completion.
/// Returns Success after pickup (equip or skip — the item is consumed either way).
/// Returns Failure if the target item is missing or already gone.
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

        EquipmentComponent equipment = self.GetComponent<EquipmentComponent>();
        if (equipment == null)
        {
            ClearBlackboard();
            return NodeState.Failure;
        }

        ItemSO item = worldItem.item;
        Vector3 itemPosition = worldItem.transform.position;

        // Explicitly evaluate and equip (or discard) the item
        equipment.TryEquipWithAssessment(item, itemPosition);

        // Destroy the world item regardless of whether it was equipped
        if (worldItem != null)
            Object.Destroy(worldItem.gameObject);

        ClearBlackboard();

        Debug.Log($"[PickupItem] {self.name} picked up {item?.itemName}");
        return NodeState.Success;
    }

    private void ClearBlackboard()
    {
        bb.Set<WorldItem>("targetWorldItem", null);
        bb.Set<Transform>("target", null);
    }
}
