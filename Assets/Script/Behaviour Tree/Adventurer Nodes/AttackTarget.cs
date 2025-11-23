using UnityEngine;

/// <summary>
/// ACTION: Attacks target (simple raycast damage).
/// Assumes enemy has Health script with TakeDamage(float).
/// Returns Success after one attack, or use Repeat decorator for burst.
/// </summary>
public class AttackTarget : Node
{
    private float damage = 25f;
    private float attackRange = 2f;
    private float attackCooldown = 1f;
    private float lastAttackTime = 0f;

    public AttackTarget(Blackboard bb, float damage = 25f, float range = 2f, float cooldown = 1f) : base(bb)
    {
        this.damage = damage;
        this.attackRange = range;
        this.attackCooldown = cooldown;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");
        if (self == null || target == null) return NodeState.Failure;

        float dist = Vector3.Distance(self.position, target.position);
        if (dist > attackRange) return NodeState.Failure;

        if (Time.time - lastAttackTime < attackCooldown) return NodeState.Running;

        // Raycast attack
        if (Physics.Raycast(self.position, (target.position - self.position).normalized, out RaycastHit hit, attackRange))
        {
            if (hit.transform == target)
            {
                HealthComponent targetHealth = hit.transform.GetComponent<Health>();
                if (targetHealth != null)
                {
                    targetHealth.TakeDamage(damage);
                    lastAttackTime = Time.time;
                    return NodeState.Success;  // One attack per tick
                }
            }
        }

        return NodeState.Failure;
    }
}
