using UnityEngine;

/// <summary>
/// ACTION: Drinks a health potion if the hero's HP is below <see cref="_threshold"/>
/// and they have at least one health potion in their inventory slot.
///
/// Returns Success — potion was consumed (hero's HP is now restored).
/// Returns Failure — no potion available, or HP is already above threshold.
///
/// DESIGN:
///   This BT node triggers at a higher HP threshold (default 50 %) than the
///   auto-use safety net in EquipmentComponent.Update() (30 %).  The BT node
///   is the hero's "strategic" decision; the auto-use is the emergency failsafe.
///
///   Place this sequence right after Extract (priority 1) so the hero heals up
///   between encounters rather than entering combat at half health.
/// </summary>
public class UseHealthPotion : Node
{
    private readonly float _threshold;

    /// <param name="threshold">HP fraction below which the hero will drink a potion (default 0.5 = 50 %).</param>
    public UseHealthPotion(Blackboard bb, float threshold = 0.5f) : base(bb)
    {
        _threshold = threshold;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var hc = self.GetComponent<HealthComponent>();
        var eq = self.GetComponent<EquipmentComponent>();

        if (hc == null || eq == null) return NodeState.Failure;

        // Already healthy enough, or no potions — nothing to do
        float hpFraction = hc.currentHealth / hc.maxHealth;
        if (hpFraction >= _threshold) return NodeState.Failure;
        if (eq.equippedHealthPotion == null || eq.healthPotionCount <= 0) return NodeState.Failure;

        bool consumed = eq.ConsumeHealthPotion();
        if (consumed)
            Debug.Log($"[UseHealthPotion] {self.name} drank a health potion at {hpFraction:P0} HP");

        return consumed ? NodeState.Success : NodeState.Failure;
    }
}
