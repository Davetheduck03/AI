using UnityEngine;

/// <summary>
/// ACTION: Drinks a mana potion if the hero's mana is below <see cref="_threshold"/>
/// and they have at least one mana potion in their inventory slot.
///
/// Returns Success — potion was consumed (hero's mana is now restored).
/// Returns Failure — no potion available, or mana is already above threshold,
///                   or this hero has no ManaComponent / zero mana cost actions.
///
/// DESIGN:
///   This BT node triggers at a higher mana threshold (default 35 %) than the
///   auto-use safety net in EquipmentComponent.Update() (20 %).
///
///   For classes where attackManaCost and healManaCost are both 0, the node
///   always returns Failure so it is effectively a no-op — no need to remove it
///   from the tree of non-mana classes.
/// </summary>
public class UseManaPotion : Node
{
    private readonly float _threshold;

    /// <param name="threshold">Mana fraction below which the hero will drink a potion (default 0.35 = 35 %).</param>
    public UseManaPotion(Blackboard bb, float threshold = 0.35f) : base(bb)
    {
        _threshold = threshold;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var mana = self.GetComponent<ManaComponent>();
        var eq   = self.GetComponent<EquipmentComponent>();

        if (eq == null) return NodeState.Failure;

        // Skip for heroes that don't use mana at all
        if (mana == null || (mana.attackManaCost <= 0f && mana.healManaCost <= 0f))
            return NodeState.Failure;

        // Already has enough mana, or no potions — nothing to do
        if (mana.ManaFraction >= _threshold) return NodeState.Failure;
        if (eq.equippedManaPotion == null || eq.manaPotionCount <= 0) return NodeState.Failure;

        bool consumed = eq.ConsumeManaPotion();
        if (consumed)
            Debug.Log($"[UseManaPotion] {self.name} drank a mana potion at {mana.ManaFraction:P0} mana");

        return consumed ? NodeState.Success : NodeState.Failure;
    }
}
