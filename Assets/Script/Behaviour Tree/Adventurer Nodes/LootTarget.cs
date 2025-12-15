using UnityEngine;

public class LootTarget : Node
{
    private float maxLootDistance;

    public LootTarget(Blackboard bb, float maxDistance = 1.5f) : base(bb)
    {
        maxLootDistance = maxDistance;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");

        if (self == null || target == null)
            return NodeState.Failure;

        // Get lootable component
        Lootable lootable = target.GetComponent<Lootable>();
        if (lootable == null)
            return NodeState.Failure;

        // Check if already looted (done)
        if (lootable.isLooted)
        {
            Debug.Log("[LootTarget] Already looted - Success!");
            return NodeState.Success;
        }

        // Check distance FIRST (before checking if looting)
        float distance = Vector3.Distance(self.position, target.position);

        if (distance > maxLootDistance)
        {
            // Too far - cancel if looting
            if (lootable.isLooting)
            {
                Debug.Log($"[LootTarget] Moved too far ({distance:F2}m) - cancelling loot");
                lootable.CancelLoot();
            }
            return NodeState.Failure;
        }

        // Within range - check if already looting
        if (lootable.isLooting)
        {
            Debug.Log($"[LootTarget] Looting in progress... ({distance:F2}m)");
            return NodeState.Running;
        }

        // Start looting (we're in range and not looting yet)
        Debug.Log($"[LootTarget] Starting loot at distance {distance:F2}m");
        lootable.Loot();
        return NodeState.Running;
    }
}