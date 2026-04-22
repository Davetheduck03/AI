using UnityEngine;

/// <summary>
/// CONDITION: Returns Success when at least one dedicated Healer (HealerAI) is alive
/// in the party.
///
/// Used by the Paladin to detect whether it must cover the healer role:
///   IsHealerAlive(bb)               → Success = a live healer is present.
///   Inverter { IsHealerAlive(bb) }  → Success = NO healer alive (Paladin is last lifeline).
///
/// "Alive" means the HealerAI's HealthComponent has currentHealth > 0.
/// The check is intentionally cheap — FindGameObjectsWithTag is cached by Unity.
/// </summary>
public class IsHealerAlive : Node
{
    public IsHealerAlive(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            if (p == null) continue;
            if (p.GetComponent<HealerAI>() == null) continue;

            var hc = p.GetComponent<HealthComponent>();
            if (hc != null && hc.currentHealth > 0f)
                return NodeState.Success;
        }

        return NodeState.Failure;   // no living HealerAI found
    }
}
