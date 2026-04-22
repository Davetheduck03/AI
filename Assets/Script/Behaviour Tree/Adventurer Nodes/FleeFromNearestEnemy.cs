using UnityEngine;

/// <summary>
/// ACTION: Fuzzy fear-driven flee behaviour.
///
/// HOW IT WORKS
///   Each tick the node computes a fear score from the hero's current HP:
///
///     fear = RampDown(hpFraction, loHP=0.15, hiHP=0.50)
///            → 1.0 when nearly dead, fades to 0 at 50 % HP or above.
///
///   If fear ≥ threshold (default 0.35) AND at least one enemy is within
///   detectionRange, the hero sprints away from the nearest enemy using
///   A*-backed pathfinding.
///
/// INTERRUPTION
///   The root Selector is REACTIVE (evaluates from first child every tick).
///   Placing FleeFromNearestEnemy BEFORE the attack branch guarantees it
///   interrupts ongoing combat the moment the hero's HP falls into the fear
///   band — the attack Sequence's Running state is simply never reached
///   because the Selector stops at this node first.
///
/// FLEE TARGET
///   A hidden child Transform ("FleeMarker") is parented to the hero and
///   repositioned to the flee destination each tick.  This lets us reuse the
///   standard MovementComponent.OnTriggerMove API without allocating new
///   GameObjects on every call.  The marker is auto-destroyed with the hero.
///
/// RECOVERY
///   Once fear drops below threshold (healer tops up the hero, or enemies
///   are out of range) the node returns Failure and the BT falls through to
///   the combat branch — the hero re-engages automatically.
///
/// CLASS TUNING (shared defaults — tweak per-class in each AI's BuildTree)
///   loHP = 0.15  → fear is maximal at 15 % HP
///   hiHP = 0.50  → fear reaches 0 at 50 % HP
///   threshold = 0.35 → flee kicks in at ~43 % HP
///                      (RampDown gives 0.35 at hp ≈ 0.43 on the default curve)
/// </summary>
public class FleeFromNearestEnemy : Node
{
    private readonly float     _loHP;
    private readonly float     _hiHP;
    private readonly float     _threshold;
    private readonly float     _detectionRange;
    private readonly float     _fleeDistance;
    private readonly LayerMask _wallLayers;

    // Throttle: re-request a new flee path at most once every N seconds so we
    // don't hammer the A* solver while already moving.
    private const float FleePathInterval = 0.35f;
    private float       _lastFleeTime    = -999f;

    // Hidden world-space marker — cached once, repositioned every tick.
    private Transform _fleeMarker;

    /// <param name="loHPFraction">   HP fraction at which fear reaches 1 (fully panicked).</param>
    /// <param name="hiHPFraction">   HP fraction at which fear reaches 0 (calm).</param>
    /// <param name="threshold">      Minimum fear score required to trigger fleeing.</param>
    /// <param name="detectionRange"> World-unit radius the hero scans for enemies.</param>
    /// <param name="fleeDistance">   How far (world units) the hero tries to put between
    ///                               itself and the nearest enemy each path request.</param>
    /// <param name="wallLayers">     Currently unused — reserved for future LOS checks.</param>
    public FleeFromNearestEnemy(Blackboard bb,
                                float      loHPFraction   = 0.15f,
                                float      hiHPFraction   = 0.50f,
                                float      threshold      = 0.35f,
                                float      detectionRange = 12f,
                                float      fleeDistance   = 6f,
                                LayerMask  wallLayers     = default) : base(bb)
    {
        _loHP           = loHPFraction;
        _hiHP           = hiHPFraction;
        _threshold      = threshold;
        _detectionRange = detectionRange;
        _fleeDistance   = fleeDistance;
        _wallLayers     = wallLayers;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        // ── Fear score ───────────────────────────────────────────────────────
        var hc = self.GetComponent<HealthComponent>();
        if (hc == null) return NodeState.Failure;

        float hpFraction = hc.maxHealth > 0f ? hc.currentHealth / hc.maxHealth : 0f;
        float fear       = FuzzyLogic.RampDown(hpFraction, _loHP, _hiHP);

        if (fear < _threshold)
            return NodeState.Failure;   // healthy enough — fall through to combat

        // ── Nearest enemy ─────────────────────────────────────────────────────
        Transform nearest = FindNearestEnemy(self);
        if (nearest == null)
            return NodeState.Failure;   // nobody to flee from

        // ── Flee destination ──────────────────────────────────────────────────
        Vector2 selfPos  = self.position;
        Vector2 enemyPos = nearest.position;
        Vector2 away     = selfPos - enemyPos;

        // If somehow the hero is exactly on top of the enemy, pick a random direction.
        if (away.sqrMagnitude < 0.001f)
            away = Random.insideUnitCircle.normalized;

        Vector2 fleeWorldPos = selfPos + away.normalized * _fleeDistance;

        // Snap the destination to the nearest walkable grid node so the pathfinder
        // doesn't receive an out-of-bounds or unwalkable target.
        var grid = GridGenerator.Instance;
        if (grid != null)
        {
            PathNode fleeNode = grid.GetNodeAtWorldPosition(fleeWorldPos)
                             ?? grid.GetNearestWalkableNode(fleeWorldPos, maxSearchRadius: 10);
            if (fleeNode != null)
                fleeWorldPos = (Vector2)fleeNode.transform.position;
        }

        // ── Move toward flee destination ──────────────────────────────────────
        EnsureFleeMarker(self);
        _fleeMarker.position = fleeWorldPos;

        if (Time.time - _lastFleeTime >= FleePathInterval)
        {
            _lastFleeTime = Time.time;

            var mc = self.GetComponent<MovementComponent>();
            if (mc != null)
                mc.OnTriggerMove(self, _fleeMarker);
        }

        Debug.Log($"[FleeFromNearestEnemy] {self.name} fear={fear:F2} (HP {hpFraction:P0})" +
                  $" — fleeing from {nearest.name}");

        return NodeState.Running;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Transform FindNearestEnemy(Transform self)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform    nearest = null;
        float        bestDist = float.MaxValue;

        foreach (var e in enemies)
        {
            if (e == null) continue;

            // Skip dead enemies (still present during death animation).
            var ehc = e.GetComponent<HealthComponent>();
            if (ehc != null && ehc.currentHealth <= 0f) continue;

            float dist = Vector2.Distance(self.position, e.transform.position);
            if (dist > _detectionRange) continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                nearest  = e.transform;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Lazily creates a hidden child Transform parented to the hero.
    /// It is destroyed automatically when the hero GameObject is destroyed.
    /// </summary>
    private void EnsureFleeMarker(Transform hero)
    {
        if (_fleeMarker != null) return;

        var go = new GameObject($"[FleeMarker]")
        {
            hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector
        };
        go.transform.SetParent(hero, worldPositionStays: false);
        _fleeMarker = go.transform;
    }
}
