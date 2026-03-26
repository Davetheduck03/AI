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

        // Chest may have been destroyed (Unity fake-null) after loot completed
        if (self == null || target == null)
        {
            bb.Set("isLooting", false);
            return NodeState.Failure;
        }

        Lootable lootable = target.GetComponent<Lootable>();
        if (lootable == null)
        {
            bb.Set("isLooting", false);
            return NodeState.Failure;
        }

        // Already looted — clean up and report success
        if (lootable.isLooted)
        {
            lootable.ReleaseClaim(self);
            bb.Set("isLooting", false);
            Debug.Log("[LootTarget] Already looted - Success!");
            return NodeState.Success;
        }

        // Distance check comes before anything else
        float distance = Vector3.Distance(self.position, target.position);

        if (distance > maxLootDistance)
        {
            // Only cancel the loot animation if WE started it
            if (lootable.isLooting && lootable.claimedBy == self)
            {
                Debug.Log($"[LootTarget] Moved too far ({distance:F2}m) — cancelling loot");
                lootable.CancelLoot();
            }
            lootable.ReleaseClaim(self);
            bb.Set("isLooting", false);
            return NodeState.Failure;
        }

        // In range — make sure we own the claim (another hero may have claimed it
        // while this hero was walking over, e.g. they're closer and got here first)
        if (!lootable.TryClaim(self))
        {
            Debug.Log("[LootTarget] Chest already claimed by another hero — giving up.");
            bb.Set("isLooting", false);
            return NodeState.Failure;
        }

        // Loot animation already running (we started it a previous tick)
        if (lootable.isLooting)
        {
            bb.Set("isLooting", true);
            Debug.Log($"[LootTarget] Looting in progress... ({distance:F2}m)");
            return NodeState.Running;
        }

        // Kick off the loot animation
        Debug.Log($"[LootTarget] Starting loot at distance {distance:F2}m");
        lootable.Loot();
        bb.Set("isLooting", true);
        return NodeState.Running;
    }
}