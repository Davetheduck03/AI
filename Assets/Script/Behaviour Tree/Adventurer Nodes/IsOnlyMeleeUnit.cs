using UnityEngine;

/// <summary>
/// CONDITION: Returns Success when this hero is the sole surviving melee unit in the party.
///
/// "Melee unit" = any alive Player tagged GameObject with a KnightAI or PaladinAI component.
/// Both classes form the frontline; if only one remains the party is dangerously exposed.
///
/// Used by the Knight to dynamically switch to a tighter FuzzyHPGuard retreat threshold —
/// the last frontliner must not go down and leave the squishies unprotected.
///
/// Returns Success — this unit is alone on the frontline (count ≤ 1).
/// Returns Failure — at least one other melee ally is alive.
/// </summary>
public class IsOnlyMeleeUnit : Node
{
    public IsOnlyMeleeUnit(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        int meleeCount = 0;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (p == null) continue;

            var hc = p.GetComponent<HealthComponent>();
            if (hc == null || hc.currentHealth <= 0f) continue;

            if (p.GetComponent<KnightAI>() != null || p.GetComponent<PaladinAI>() != null)
                meleeCount++;
        }

        // ≤ 1 means only this unit (or nobody), so we are alone on the frontline.
        return meleeCount <= 1 ? NodeState.Success : NodeState.Failure;
    }
}
