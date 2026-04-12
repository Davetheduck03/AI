using UnityEngine;

/// <summary>
/// ACTION: Moves into heal range and casts a heal on the ally stored in bb[targetKey].
///
/// Heal power is sourced from the caster's own DamageComponent.TotalDamage so that
/// stat upgrades and equipment affect healing the same way they affect damage.
/// The heal cooldown reuses AttackCooldown for the same reason.
///
/// Returns Running  — closing into range or waiting out cooldown.
/// Returns Success  — a heal was applied this tick.
/// Returns Failure  — target is null/dead, already at full HP, or unreachable.
/// </summary>
public class HealTarget : Node
{
    private readonly float  _healRange;
    private readonly string _targetKey;

    private Transform _lastTarget        = null;
    private float     _lastHealTime      = 0f;
    private float     _movementStartTime = 0f;
    private float     _nextMoveCheck     = 0f;
    private Vector3   _lastTriggeredPos  = Vector3.zero;

    private const float MovementTimeout      = 6f;
    private const float MoveCheckInterval    = 0.3f;
    private const float TargetDriftThreshold = 0.6f;  // re-path if target moved this far
    private const float RetryDelay           = 3f;    // seconds before retrying a timed-out target

    // Target we gave up on, and when we're allowed to try again.
    private Transform _blockedTarget = null;
    private float     _retryAfter    = 0f;

    /// <param name="healRange">World-unit radius within which the heal cast fires.</param>
    /// <param name="targetKey">Blackboard key holding the heal target's Transform.</param>
    public HealTarget(Blackboard bb, float healRange = 2.5f, string targetKey = "healTarget") : base(bb)
    {
        _healRange = healRange;
        _targetKey = targetKey;
    }

    public override NodeState Evaluate()
    {
        Transform self   = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>(_targetKey);

        // ── Guard: target must be alive ───────────────────────────────────────
        if (self == null || target == null || target.gameObject == null)
            return NodeState.Failure;

        // If we recently timed out on this exact target, don't retry yet --
        // prevents the infinite 4-second chase loop when an ally is far away.
        if (target == _blockedTarget && Time.time < _retryAfter)
            return NodeState.Failure;

        var targetHC = target.GetComponent<HealthComponent>();
        if (targetHC == null || targetHC.currentHealth >= targetHC.maxHealth)
        {
            // Target is full HP — job done, release the reference
            bb.Set<Transform>(_targetKey, null);
            _lastTarget = null;
            return NodeState.Failure;
        }

        if (!self.TryGetComponent<DamageComponent>(out var dc))
            return NodeState.Failure;

        float dist = Vector2.Distance(self.position, target.position);

        // ── Trigger movement: new target, or target drifted while we're closing ──
        bool isNewTarget   = target != _lastTarget;
        bool targetDrifted = dist > _healRange
                          && Time.time >= _nextMoveCheck
                          && Vector3.Distance(target.position, _lastTriggeredPos) > TargetDriftThreshold;

        if (isNewTarget || targetDrifted)
        {
            if (isNewTarget)
                _movementStartTime = Time.time;

            _lastTarget        = target;
            _lastTriggeredPos  = target.position;
            _nextMoveCheck     = Time.time + MoveCheckInterval;
            TriggerMove(self, target);
            return NodeState.Running;
        }

        // ── Still closing in ──────────────────────────────────────────────────
        if (dist > _healRange)
        {
            if (Time.time - _movementStartTime > MovementTimeout)
            {
                Debug.Log($"[HealTarget] {self.name} timed out reaching {target.name} — giving up");
                StopMove(self);
                _blockedTarget = target;
                _retryAfter    = Time.time + RetryDelay;
                bb.Set<Transform>(_targetKey, null);
                _lastTarget = null;
                return NodeState.Failure;
            }
            return NodeState.Running;
        }

        // ── In range — stop moving and cast ───────────────────────────────────
        StopMove(self);

        if (Time.time - _lastHealTime < dc.AttackCooldown)
            return NodeState.Running;   // waiting out cooldown

        float healAmount = dc.TotalDamage;
        targetHC.Heal(healAmount);
        _lastHealTime = Time.time;

        Debug.Log($"[HealTarget] {self.name} healed {target.name} for {healAmount:F1} HP");
        return NodeState.Success;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TriggerMove(Transform self, Transform target)
    {
        StopMove(self);
        self.GetComponent<MovementComponent>()?.OnTriggerMove(self, target);
    }

    private void StopMove(Transform self)
    {
        self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
    }
}
