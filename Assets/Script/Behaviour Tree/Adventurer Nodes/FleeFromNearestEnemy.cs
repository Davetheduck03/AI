using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ACTION: Fuzzy fear-driven flee behaviour with hysteresis and smart direction.
///
/// FEAR SCORE
///   fear = RampDown(hpFraction, loHP=0.15, hiHP)
///   Each class passes its own hiHP so personality differences are preserved.
///
/// HYSTERESIS
///   enterThreshold (default 0.35) — fear must EXCEED this to START fleeing.
///   exitThreshold  (default 0.07) — fear must DROP BELOW this to STOP fleeing.
///   Hero will not re-engage until healed close to full HP.
///
/// SMART FLEE DIRECTION
///   Instead of blindly running opposite the nearest enemy, the node samples
///   16 evenly-spaced directions and scores each by the SUM of distances from
///   the candidate position to ALL nearby enemies. The highest-scoring direction
///   is the one leading to the largest enemy-free space — even if it means
///   sprinting THROUGH a gap between enemies to reach it.
///
///   Because enemies are not pathfinding obstacles (only walls are), A* will
///   route the actual path through enemy-occupied tiles. The hero simply runs
///   past them without fighting — flee has higher priority than attack.
///
/// CLUSTER-AWARE RE-PATHING
///   Re-path triggers are based on the CENTROID of all nearby enemies, not just
///   the nearest one. This prevents thrashing when enemies mill around each other
///   while the centroid stays stable.
///
/// HOLD AT SAFE SPOT
///   Once the hero arrives, it holds position — returns Running, waits for HP
///   to recover. A new path is only requested when the cluster centroid moves > 1.5u.
///
/// PROGRESS CHECKER EXEMPTION
///   "flee" is excluded from stuckable phases so the hero at a safe spot is not
///   force-reset.
/// </summary>
public class FleeFromNearestEnemy : Node
{
    // ── Configuration ─────────────────────────────────────────────────────────
    private readonly float     _loHP;
    private readonly float     _hiHP;
    private readonly float     _enterThreshold;
    private readonly float     _exitThreshold;
    private readonly float     _detectionRange;
    private readonly float     _fleeDistance;
    private readonly LayerMask _wallLayers;

    // ── Hysteresis state ──────────────────────────────────────────────────────
    private bool _isFleeing = false;

    // ── Path throttle ─────────────────────────────────────────────────────────
    private const float FleePathInterval = 0.40f;
    private float       _lastFleeTime    = -999f;

    // ── Cluster tracking ──────────────────────────────────────────────────────
    private Vector2 _lastClusterWhenPathed   = Vector2.positiveInfinity;
    private const float ClusterMoveThreshold = 1.5f;   // world units before re-pathing

    // ── Direction sampling ────────────────────────────────────────────────────
    private const int NumDirections = 16;

    // ── Flee marker ───────────────────────────────────────────────────────────
    private Transform _fleeMarker;

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
        _enterThreshold = threshold;
        _exitThreshold  = threshold * 0.20f;
        _detectionRange = detectionRange;
        _fleeDistance   = fleeDistance;
        _wallLayers     = wallLayers;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        // ── Fear score ────────────────────────────────────────────────────────
        var hc = self.GetComponent<HealthComponent>();
        if (hc == null) return NodeState.Failure;

        float hpFraction = hc.maxHealth > 0f ? hc.currentHealth / hc.maxHealth : 0f;
        float fear       = FuzzyLogic.RampDown(hpFraction, _loHP, _hiHP);

        // ── Hysteresis gate ───────────────────────────────────────────────────
        if (!_isFleeing)
        {
            if (fear < _enterThreshold)
                return NodeState.Failure;
            _isFleeing = true;
            Debug.Log($"[Flee] {self.name} ENTERED flee " +
                      $"(fear={fear:F2} ≥ {_enterThreshold:F2}, HP {hpFraction:P0})");
        }
        else
        {
            if (fear < _exitThreshold)
            {
                _isFleeing = false;
                _lastClusterWhenPathed = Vector2.positiveInfinity;
                Debug.Log($"[Flee] {self.name} EXITED flee " +
                          $"(fear={fear:F2} < {_exitThreshold:F2}, HP {hpFraction:P0})");
                return NodeState.Failure;
            }
        }

        // ── Collect all nearby enemies ────────────────────────────────────────
        List<Vector2> enemyPositions = CollectEnemyPositions(self.position);

        if (enemyPositions.Count == 0)
        {
            // No enemies in range — hold at current position, wait to heal
            return NodeState.Running;
        }

        // ── Cluster centroid ──────────────────────────────────────────────────
        Vector2 centroid = Vector2.zero;
        foreach (var ep in enemyPositions) centroid += ep;
        centroid /= enemyPositions.Count;

        float clusterDelta    = Vector2.Distance(centroid, _lastClusterWhenPathed);
        bool  enoughTime      = Time.time - _lastFleeTime >= FleePathInterval;
        bool  clusterMoved    = clusterDelta >= ClusterMoveThreshold;

        // Re-path when both the interval has elapsed AND the cluster has shifted
        if (enoughTime && clusterMoved)
        {
            Vector2 selfPos   = self.position;
            Vector2 safestDir = ComputeSafestDirection(selfPos, enemyPositions);
            Vector2 fleePos   = selfPos + safestDir * _fleeDistance;

            // Snap to nearest walkable node
            var grid = GridGenerator.Instance;
            if (grid != null)
            {
                PathNode node = grid.GetNodeAtWorldPosition(fleePos)
                             ?? grid.GetNearestWalkableNode(fleePos, maxSearchRadius: 10);
                if (node != null)
                    fleePos = node.transform.position;
            }

            EnsureFleeMarker(self);
            _fleeMarker.position    = fleePos;
            _lastFleeTime           = Time.time;
            _lastClusterWhenPathed  = centroid;

            var mc = self.GetComponent<MovementComponent>();
            if (mc != null)
                mc.OnTriggerMove(self, _fleeMarker);

            Debug.Log($"[Flee] {self.name} re-pathing → {fleePos} " +
                      $"(fear={fear:F2}, {enemyPositions.Count} enemies, " +
                      $"safest dir={safestDir})");
        }

        return NodeState.Running;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Samples <see cref="NumDirections"/> evenly-spaced directions and returns
    /// the one whose candidate position (self + dir × fleeDistance) maximises
    /// the sum of distances to all nearby enemies.
    ///
    /// Scoring all enemies simultaneously means:
    ///   • Surrounded heroes find the gap in the enemy ring.
    ///   • A hero fleeing one enemy picks the direction most clear of everyone.
    ///   • The chosen direction may pass THROUGH one enemy to reach open space
    ///     behind them — A* will path through (enemies are not wall obstacles).
    /// </summary>
    private Vector2 ComputeSafestDirection(Vector2 selfPos, List<Vector2> enemies)
    {
        Vector2 bestDir   = Vector2.down;
        float   bestScore = float.MinValue;

        for (int i = 0; i < NumDirections; i++)
        {
            float   angle     = i * (360f / NumDirections) * Mathf.Deg2Rad;
            Vector2 dir       = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 candidate = selfPos + dir * _fleeDistance;

            // Sum distances from candidate to every nearby enemy.
            // Higher total = further from the whole threat cluster.
            float score = 0f;
            foreach (var ePos in enemies)
                score += Vector2.Distance(candidate, ePos);

            if (score > bestScore)
            {
                bestScore = score;
                bestDir   = dir;
            }
        }

        return bestDir;
    }

    private List<Vector2> CollectEnemyPositions(Vector2 origin)
    {
        var result  = new List<Vector2>();
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (var e in enemies)
        {
            if (e == null) continue;
            var ehc = e.GetComponent<HealthComponent>();
            if (ehc != null && ehc.currentHealth <= 0f) continue;

            float dist = Vector2.Distance(origin, e.transform.position);
            if (dist <= _detectionRange)
                result.Add(e.transform.position);
        }

        return result;
    }

    private void EnsureFleeMarker(Transform hero)
    {
        if (_fleeMarker != null) return;
        var go = new GameObject("[FleeMarker]")
        {
            hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector
        };
        go.transform.SetParent(hero, worldPositionStays: false);
        _fleeMarker = go.transform;
    }
}
