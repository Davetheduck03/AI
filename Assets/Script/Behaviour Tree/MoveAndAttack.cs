// MoveAndAttack.cs
using UnityEngine;

/// <summary>
/// Combines move + attack into one node for both adventurers and enemies.
/// Re-triggers movement if attack fails (blocked LOS, range gap, target drift).
/// </summary>
public class MoveAndAttack : Node
{
    private LayerMask targetLayer;
    // float.MinValue means Time.time - float.MinValue is always >> any AttackCooldown,
    // so the first attack fires immediately regardless of when in the session this
    // unit first acquires a target (avoids the false cooldown from initialising to 0f
    // when Time.time < AttackCooldown at game start).
    private float lastAttackTime = float.MinValue;
    private Transform lastTarget = null;
    private bool arrived = false;

    // If the hero hasn't arrived within this many seconds of triggering
    // movement, the target is considered unreachable and we return Failure
    // so the BT can fall through to other behaviours.
    private const float MovementTimeout = 4f;
    private float movementStartTime = 0f;

    /// <summary>
    /// Approach distance is no longer a constructor parameter — it is read live from
    /// DamageComponent.AttackRange each frame so equipping a different weapon
    /// automatically changes how close the unit gets before attacking.
    /// </summary>
    public MoveAndAttack(Blackboard bb, LayerMask targetLayer) : base(bb)
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
            return NodeState.Failure;

        if (target.gameObject == null)
            return NodeState.Failure;

        // Same non-reactive Sequence issue as KiteAndAttack: while MoveAndAttack
        // is Running the Sequence skips SelectCombatTarget, so a corpse (HP = 0
        // but GO alive during death animation) is never detected by SelectCombatTarget.
        // Bail out here immediately rather than chasing the corpse for MovementTimeout (4 s).
        var targetHp = target.GetComponent<HealthComponent>();
        if (targetHp != null && targetHp.currentHealth <= 0)
        {
            arrived    = false;
            lastTarget = null;
            StopMovement(self);
            bb.Set<Transform>("target", null);
            return NodeState.Failure;
        }

        float dist = Vector2.Distance(self.position, target.position);

        // Approach distance comes from the unit's current weapon range — updates
        // automatically when a new weapon is equipped mid-run.
        float effectiveRange = damageComp.AttackRange;

        // New target — check if already in range before triggering pathfinding.
        // The old code always called TriggerMovement + returned Running on first acquisition,
        // even when the target was right next to the unit.  This wasted one full BT tick
        // and could restart a path that immediately gets cancelled.
        if (target != lastTarget)
        {
            lastTarget = target;
            if (dist <= effectiveRange)
            {
                // Already in attack range — skip pathfinding, fall through to attack now.
                arrived = true;
            }
            else
            {
                arrived = false;
                movementStartTime = Time.time;
                TriggerMovement(self, target);
                return NodeState.Running;
            }
        }

        // Target drifted away after arrival — re-trigger movement.
        // Use a generous buffer so minor enemy wander / residual separation don't
        // immediately restart the path and create a visible twitch.
        if (arrived && dist > effectiveRange * 1.6f)
        {
            arrived = false;
            movementStartTime = Time.time;
            TriggerMovement(self, target);
            return NodeState.Running;
        }

        // Still moving toward target
        if (!arrived)
        {
            if (dist <= effectiveRange)
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
                    bb.Set<Transform>("target", null);
                    bb.Set<Transform>("itemTarget", null);
                    bb.Set<WorldItem>("targetWorldItem", null);
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

        // Re-trigger only if hero has drifted well past attack range.
        // The old threshold was exactly `range` — any push of even 0.001 u beyond it
        // restarted the path and caused twitching.  Match the generous buffer used
        // by the drift check above so both code paths stay consistent.
        if (dist > effectiveRange * 1.6f)
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
            StopMovement(self);
            bb.Set<Transform>("target", null);
            return NodeState.Success;
        }

        // Single-target attack — deal damage directly.
        string targetName = target.name;
        damageComp.TryDealDamage(target.gameObject);
        lastAttackTime = Time.time;
        Debug.Log($"[MoveAndAttack] {self.name} hit {targetName} (dist: {dist:F2})");

        // If the attack killed the target, stop movement and clear the blackboard
        // immediately so lower-priority sequences don't read a stale reference.
        if (target == null || target.gameObject == null)
        {
            arrived = false;
            lastTarget = null;
            StopMovement(self);
            bb.Set<Transform>("target", null);
            bb.Set<Transform>("itemTarget", null);
            bb.Set<WorldItem>("targetWorldItem", null);
        }

        return NodeState.Success;
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
            pathFollower.StopPath();
    }
}