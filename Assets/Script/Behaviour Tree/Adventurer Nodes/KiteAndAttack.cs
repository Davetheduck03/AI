// KiteAndAttack.cs
using UnityEngine;

/// <summary>
/// Kiting combat node for ranged units (archers).
///
/// State machine:
///   RETREATING  — enemy is inside <kiteDistance>; back away while still attacking.
///   STRAFING    — enemy is between <kiteDistance> and <attackRange>; hold position
///                 and fire.  Periodically side-step to break AI prediction.
///   CLOSING     — enemy is beyond <attackRange>; close until within range.
///
/// Movement is handled by calling MovementComponent.OnTriggerMove with a
/// dynamically placed temporary target, exactly like the rest of the BT system.
/// The node returns Running while combat is ongoing and Success after each
/// successful hit (so the BT can re-evaluate priorities).
/// Returns Failure if the target is lost or we time out trying to reach it.
/// </summary>
public class KiteAndAttack : Node
{
    // ── Tuning ───────────────────────────────────────────────────────────────

    /// <summary>Preferred distance to maintain from the enemy.</summary>
    private readonly float kiteDistance;

    /// <summary>
    /// How far inside kiteDistance the enemy must be before we start retreating.
    /// Small deadzone prevents jittery back-and-forth.
    /// </summary>
    private const float RetreaTriggerMargin = 0.3f;

    /// <summary>How far outside attackRange before we start closing in.</summary>
    private const float CloseInMargin = 0.5f;

    /// <summary>Seconds between strafe direction changes.</summary>
    private const float StrafeInterval = 1.8f;

    /// <summary>How far to the side to strafe each interval (world units).</summary>
    private const float StrafeDistance = 2.5f;

    /// <summary>
    /// If the unit hasn't moved this far within MovementCheckInterval seconds,
    /// the retreat target is unreachable (wall behind us) — stop trying.
    /// </summary>
    private const float StuckDistanceThreshold = 0.25f;
    private const float MovementCheckInterval  = 2.0f;

    // ── State ─────────────────────────────────────────────────────────────────

    private enum CombatState { Closing, Strafing, Retreating }
    private CombatState _state = CombatState.Closing;

    private readonly LayerMask _targetLayer;
    private float _lastAttackTime  = 0f;
    private float _nextStrafeTime  = 0f;
    private int   _strafeDirection = 1;   // +1 or -1

    // Movement target reuse
    private Transform _lastEnemy     = null;
    private GameObject _moveTargetGO = null;

    // Stuck detection
    private Vector3 _lastCheckedPos   = Vector3.zero;
    private float   _nextStuckCheckAt = 0f;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <param name="kiteDistance">Desired distance to keep from the enemy.</param>
    public KiteAndAttack(Blackboard bb, LayerMask targetLayer, float kiteDistance = 3.5f)
        : base(bb)
    {
        this.kiteDistance = kiteDistance;
        _targetLayer      = targetLayer;
    }

    // ── Evaluate ──────────────────────────────────────────────────────────────

    public override NodeState Evaluate()
    {
        Transform self   = bb.Get<Transform>("self");
        Transform target = bb.Get<Transform>("target");

        if (self == null || target == null || target.gameObject == null)
        {
            Cleanup(self);
            return NodeState.Failure;
        }

        if (!self.TryGetComponent<DamageComponent>(out var damageComp))
            return NodeState.Failure;

        // Snap kite distance to weapon range - 0.5 so we are always within attack range.
        float effectiveAttackRange = damageComp.AttackRange;
        float effectiveKiteRange   = Mathf.Min(kiteDistance, effectiveAttackRange - 0.3f);

        float dist = Vector2.Distance(self.position, target.position);

        // ── New target → reset ────────────────────────────────────────────────
        if (target != _lastEnemy)
        {
            _lastEnemy        = target;
            _state            = CombatState.Closing;
            _nextStrafeTime   = Time.time + StrafeInterval;
            _lastCheckedPos   = self.position;
            _nextStuckCheckAt = Time.time + MovementCheckInterval;
        }

        // ── Determine state ───────────────────────────────────────────────────
        if (dist < effectiveKiteRange - RetreaTriggerMargin)
        {
            _state = CombatState.Retreating;
        }
        else if (dist > effectiveAttackRange + CloseInMargin)
        {
            _state = CombatState.Closing;
        }
        else
        {
            // In the sweet-spot band between kiteRange and attackRange
            _state = CombatState.Strafing;
        }

        // ── Act on state ──────────────────────────────────────────────────────
        switch (_state)
        {
            case CombatState.Closing:
                MoveTowards(self, target.position);
                break;

            case CombatState.Retreating:
                if (!TryRetreat(self, target))
                    // Nowhere to run — just hold and shoot
                    StopMovement(self);
                break;

            case CombatState.Strafing:
                TryStrafe(self, target);
                break;
        }

        // ── Attack whenever in range and off cooldown ─────────────────────────
        bool inRange = dist <= effectiveAttackRange;
        if (inRange && Time.time - _lastAttackTime >= damageComp.AttackCooldown)
        {
            damageComp.TryDealDamage(target.gameObject);
            _lastAttackTime = Time.time;
            Debug.Log($"[KiteAndAttack] {self.name} shot {target.name} from {dist:F2} " +
                      $"(state: {_state})");
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    // ── Movement helpers ──────────────────────────────────────────────────────

    /// <summary>Move directly toward a world position.</summary>
    private void MoveTowards(Transform self, Vector3 worldPos)
    {
        SetMoveTarget(self, worldPos);
    }

    /// <summary>
    /// Place a retreat target behind us (away from enemy).
    /// Returns false if we detect we're stuck (wall behind us).
    /// </summary>
    private bool TryRetreat(Transform self, Transform enemy)
    {
        Vector3 awayDir = (self.position - enemy.position).normalized;

        // How far to step back — enough to clear the kite deadzone
        float stepBack = kiteDistance - Vector2.Distance(self.position, enemy.position)
                         + RetreaTriggerMargin + 1.0f;
        stepBack = Mathf.Max(stepBack, 1.5f);

        Vector3 retreatPos = self.position + awayDir * stepBack;

        // Stuck check — if we haven't moved since the last interval, give up retreating
        if (Time.time >= _nextStuckCheckAt)
        {
            float moved = Vector3.Distance(self.position, _lastCheckedPos);
            if (moved < StuckDistanceThreshold && _state == CombatState.Retreating)
            {
                Debug.Log($"[KiteAndAttack] {self.name} stuck while retreating — holding position.");
                _lastCheckedPos   = self.position;
                _nextStuckCheckAt = Time.time + MovementCheckInterval;
                return false;
            }
            _lastCheckedPos   = self.position;
            _nextStuckCheckAt = Time.time + MovementCheckInterval;
        }

        SetMoveTarget(self, retreatPos);
        return true;
    }

    /// <summary>
    /// Periodically step sideways to dodge predictive attacks.
    /// </summary>
    private void TryStrafe(Transform self, Transform enemy)
    {
        if (Time.time < _nextStrafeTime)
        {
            // Between strafes — stop so we don't drift out of range
            StopMovement(self);
            return;
        }

        _nextStrafeTime  = Time.time + StrafeInterval;
        _strafeDirection = -_strafeDirection;   // alternate sides

        // Perpendicular to the enemy direction
        Vector3 toEnemy   = (enemy.position - self.position).normalized;
        Vector3 perpDir   = new Vector3(-toEnemy.y, toEnemy.x, 0f) * _strafeDirection;
        Vector3 strafePos = self.position + perpDir * StrafeDistance;

        Debug.Log($"[KiteAndAttack] {self.name} strafing {(_strafeDirection > 0 ? "right" : "left")}");
        SetMoveTarget(self, strafePos);
    }

    // ── Move-target management ────────────────────────────────────────────────

    private void SetMoveTarget(Transform self, Vector3 worldPos)
    {
        // Reuse the same GO to avoid per-frame allocations
        if (_moveTargetGO == null)
            _moveTargetGO = new GameObject("KiteTarget");

        _moveTargetGO.transform.position = worldPos;
        bb.Set("target", _moveTargetGO.transform);

        var mc = self.GetComponent<MovementComponent>();
        mc?.OnTriggerMove(self, _moveTargetGO.transform);
    }

    private void StopMovement(Transform self)
    {
        self.GetComponent<UnitPathFollower>()?.StopAllCoroutines();
    }

    private void Cleanup(Transform self)
    {
        StopMovement(self);
        if (_moveTargetGO != null)
        {
            Object.Destroy(_moveTargetGO);
            _moveTargetGO = null;
        }
        _lastEnemy = null;
    }
}
