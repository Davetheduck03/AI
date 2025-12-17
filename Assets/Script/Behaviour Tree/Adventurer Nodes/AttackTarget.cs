using UnityEngine;

/// <summary>
/// ACTION: Attacks target using 2D raycast.
/// Damage is pulled from DamageComponent on the attacker.
/// Returns Success after one attack.
/// </summary>
public class AttackTarget : Node
{
    private float attackRange;
    private float attackCooldown;
    private float lastAttackTime = 0f;
    private LayerMask targetLayer;

    public AttackTarget(Blackboard bb, float range = 2f, float cooldown = 1f, LayerMask targetLayer = default) : base(bb)
    {
        this.attackRange = range;
        this.attackCooldown = cooldown;
        this.targetLayer = targetLayer;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");

        if (self == null || target == null)
        {
            Debug.Log("Self or Target Not Found");
            return NodeState.Failure;
        }

        Vector2 selfPos2D = (Vector2)self.position;
        Vector2 targetPos2D = (Vector2)target.position;
        float dist = Vector2.Distance(selfPos2D, targetPos2D);

        if (dist > attackRange)
        {
            return NodeState.Failure;
        }

        if (Time.time - lastAttackTime < attackCooldown)
        {
            return NodeState.Running;
        }

        Vector2 direction = (targetPos2D - selfPos2D).normalized;
        RaycastHit2D[] hits = Physics2D.RaycastAll(selfPos2D, direction, attackRange, targetLayer);

        if (hits.Length > 0 && hits[0].collider != null && hits[0].transform == target)
        {
            Debug.DrawRay(selfPos2D, direction * dist, Color.green, 0.5f);

            if (self.gameObject.TryGetComponent<DamageComponent>(out var damageComponent))
            {
                damageComponent.TryDealDamage(hits[0].transform.gameObject);
                lastAttackTime = Time.time;
                Debug.Log($"Enemy Hit! (dist: {dist:F1})");
                return NodeState.Success;
            }
            else
            {
                Debug.LogWarning("DamageComponent missing on attacker!");
            }
        }
        else
        {
            Debug.DrawRay(selfPos2D, direction * attackRange, Color.red, 0.5f);
            Debug.Log($"Raycast missed target (hit: {(hits.Length > 0 ? hits[0].collider?.name : "nothing")})");
        }

        return NodeState.Failure;
    }
}
