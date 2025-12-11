using UnityEngine;

/// <summary>
/// CONDITION: Checks if unit has enough health to enter a room.
/// Returns Success if health is above threshold, Failure if too low.
/// Use this to decide whether AI should enter dangerous rooms.
/// </summary>
public class HasEnoughHealth : Node
{
    private float minHealthPercent;

    public HasEnoughHealth(Blackboard bb, float minHealthPercent = 0.5f) : base(bb)
    {
        this.minHealthPercent = minHealthPercent;  // 0.5 = 50%
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        // Get health component (adjust based on your health system)
        HealthComponent healthComp = self.GetComponent<HealthComponent>();

        if (healthComp == null)
        {
            Debug.LogWarning("HasEnoughHealth: No HealthComponent found!");
            return NodeState.Success;  // Default to allowing if no health system
        }

        float currentHealthPercent = healthComp.currentHealth / healthComp.maxHealth;

        bool hasEnoughHealth = currentHealthPercent >= minHealthPercent;

        if (!hasEnoughHealth)
        {
            Debug.Log($"HasEnoughHealth: Health too low ({currentHealthPercent:P0} < {minHealthPercent:P0}) - avoiding room");
        }

        return hasEnoughHealth ? NodeState.Success : NodeState.Failure;
    }
}