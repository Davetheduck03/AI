using UnityEngine;

/// <summary>
/// ACTION: Attacks target using range, AoE, damage, and cooldown
/// all sourced from DamageComponent (which reflects UnitSO base stats + weapon bonuses).
/// Returns Success after one attack lands.
/// </summary>
public class AttackTarget : Node
{
    private LayerMask targetLayer;
    private float lastAttackTime = float.MinValue;

    // Range is no longer passed in — it's read live from DamageComponent
    public AttackTarget(Blackboard bb, LayerMask targetLayer = default) : base(bb)
    {
        this.targetLayer = targetLayer;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");

        if (self == null || target == null)
            return NodeState.Failure;

        if (!self.TryGetComponent<DamageComponent>(out var damageComp))
        {
            Debug.LogWarning("AttackTarget: DamageComponent missing!");
            return NodeState.Failure;
        }

        Vector2 selfPos2D = (Vector2)self.position;
        Vector2 targetPos2D = (Vector2)target.position;
        float dist = Vector2.Distance(selfPos2D, targetPos2D);
        float range = damageComp.AttackRange;

        // Out of range
        if (dist > range)
            return NodeState.Failure;

        // Still on cooldown
        if (Time.time - lastAttackTime < damageComp.AttackCooldown)
            return NodeState.Running;

        // ── AoE: no raycast needed, just deal damage around self ──
        if (damageComp.IsAoE)
        {
            Debug.DrawRay(selfPos2D, Vector2.up * range, Color.magenta, 0.5f);
            damageComp.TryDealDamage(target.gameObject, selfPos2D, targetLayer);
            lastAttackTime = Time.time;
            return NodeState.Success;
        }

        // ── Single-target: raycast to confirm line of sight ──
        Vector2 direction = (targetPos2D - selfPos2D).normalized;
        RaycastHit2D[] hits = Physics2D.RaycastAll(selfPos2D, direction, range, targetLayer);

        // Skip self, find target
        foreach (var hit in hits)
        {
            if (hit.transform == self) continue;
            if (hit.transform == target)
            {
                damageComp.TryDealDamage(hit.transform.gameObject);
                lastAttackTime = Time.time;
                return NodeState.Success;
            }
            break; // Something blocking LOS
        }
        return NodeState.Failure;
    }
}