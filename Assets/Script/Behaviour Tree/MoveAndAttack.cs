// MoveAndAttack.cs
using UnityEngine;

/// <summary>
/// Combines move + attack into one node for both adventurers and enemies.
/// Re-triggers movement if attack fails (blocked LOS, range gap, target drift).
/// </summary>
public class MoveAndAttack : Node
{
    private LayerMask targetLayer;
    private float approachDistance;
    private float lastAttackTime = 0f;
    private Transform lastTarget = null;
    private bool arrived = false;

    // If the hero hasn't arrived within this many seconds of triggering
    // movement, the target is considered unreachable and we return Failure
    // so the BT can fall through to other behaviours.
    private const float MovementTimeout = 4f;
    private float movementStartTime = 0f;

    public MoveAndAttack(Blackboard bb, float approachDistance, LayerMask targetLayer) : base(bb)
    {
        this.approachDistance = approachDistance;
        this.targetLayer = targetLayer;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");

        if (self == null || target == null)
            return NodeState.Failure;

        if (!self.TryGetComponent<DamageComponent>(out var damageComp))
            return NodeState.Failure;

        if (target.gameObject == null)
            return NodeState.Failure;

        float dist = Vector2.Distance(self.position, target.position);

        // New target — re-trigger movement
        if (target != lastTarget)
        {
            arrived = false;
            lastTarget = target;
            movementStartTime = Time.time;
            TriggerMovement(self, target);
            return NodeState.Running;
        }

        // Target drifted away after arrival — re-trigger movement
        if (arrived && dist > approachDistance * 1.2f)
        {
            arrived = false;
            movementStartTime = Time.time;
            TriggerMovement(self, target);
            return NodeState.Running;
        }

        // Still moving toward target
        if (!arrived)
        {
            if (dist <= approachDistance)
            {
                arrived = true;
                StopMovement(self);
            }
            else
            {
                // Give up if we've been chasing for too long without arriving —
                // A* likely found no path (target is behind a wall/in an isolated area).
                if (Time.time - movementStartTime > MovementTimeout)
                {
                    Debug.Log($"[MoveAndAttack] Timed out chasing {target?.name} — returning Failure");
                    arrived = false;
                    lastTarget = null;
                    StopMovement(self);
                    return NodeState.Failure;
                }
                return NodeState.Running;
            }
        }

        // ── Arrived — attempt attack ──

        if (Time.time - lastAttackTime < damageComp.AttackCooldown)
            return NodeState.Running;

        Vector2 selfPos2D = self.position;
        Vector2 targetPos2D = target.position;
        float range = damageComp.AttackRange;

        // Out of attack range — re-trigger movement
        if (dist > range)
        {
            arrived = false;
            TriggerMovement(self, target);
            return NodeState.Running;
        }

        // AoE attack
        if (damageComp.IsAoE)
        {
            damageComp.TryDealDamage(target.gameObject, selfPos2D, targetLayer);
            lastAttackTime = Time.time;
            return NodeState.Success;
        }

        // Single-target raycast
        Vector2 direction = (targetPos2D - selfPos2D).normalized;
        RaycastHit2D[] hits = Physics2D.RaycastAll(selfPos2D, direction, range, targetLayer);

        foreach (var hit in hits)
        {
            if (hit.transform == self) continue;
            if (hit.transform == target)
            {
                damageComp.TryDealDamage(hit.transform.gameObject);
                lastAttackTime = Time.time;
                Debug.Log($"[MoveAndAttack] {self.name} hit {target.name}");
                return NodeState.Success;
            }
            break; // Something blocking LOS
        }

        // Raycast blocked — reposition
        Debug.Log($"[MoveAndAttack] Attack blocked, repositioning...");
        arrived = false;
        TriggerMovement(self, target);
        return NodeState.Running;
    }

    private void TriggerMovement(Transform self, Transform target)
    {
        StopMovement(self);
        MovementComponent movementComp = self.GetComponent<MovementComponent>();
        if (movementComp != null)
            movementComp.OnTriggerMove(self, target);
    }

    private void StopMovement(Transform self)
    {
        UnitPathFollower pathFollower = self.GetComponent<UnitPathFollower>();
        if (pathFollower != null)
            pathFollower.StopAllCoroutines();
    }
}