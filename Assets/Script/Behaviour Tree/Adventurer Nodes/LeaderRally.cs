using UnityEngine;

/// <summary>
/// ACTION (leader only): When the party is hurt and enemies are nearby, the leader
/// finds the safest reachable position and holds there so followers can converge,
/// heal up, and share potions before re-engaging.
///
/// TRIGGER CONDITIONS (all must be true)
///   • This hero IS the formation leader.
///   • At least one ally is below partyHurtThreshold HP fraction.
///   • At least one enemy is visible within enemyScanRange.
///
/// BEHAVIOUR
///   1. Find the safest nearby position using multi-directional scoring
///      (same algorithm as the improved FleeFromNearestEnemy — maximises
///      summed distance from all nearby enemies across 16 sampled angles).
///   2. Move there.  On arrival, hold — return Running every tick so
///      lower-priority nodes (Follow, SharePotion) fire for the followers
///      who have caught up, while the leader acts as the rally anchor.
///   3. Exit when BOTH of the following are true:
///        (a) Exit conditions are met: no enemy visible OR all allies healed.
///        (b) A grace period (TransferGraceDuration) has elapsed since those
///            conditions were first satisfied.
///      The grace period lets nearby followers finish any in-flight SharePotion
///      transfers before the leader moves on. If conditions deteriorate during
///      the grace period (e.g. a new enemy appears) the countdown resets.
///
/// WHY FOLLOWERS COME
///   Followers' FollowLeader sequence fires once the leader is stationary.
///   SharePotion fires next for whoever reaches the rally point first.
///   The leader doesn't need to do anything special — just stay put.
/// </summary>
public class LeaderRally : Node
{
    private readonly float _partyHurtThreshold;  // ally HP fraction below which rally triggers
    private readonly float _enemyScanRange;       // range to check for visible enemies
    private readonly float _rallyDistance;        // how far from enemies to move
    private readonly float _holdRange;            // arrival radius at rally point

    // ── Movement state ────────────────────────────────────────────────────────
    private Transform _rallyMarker;
    private bool      _isHolding   = false;   // true once we've arrived at rally point
    private const float RePathInterval          = 0.5f;
    private const float EnemyClusterMoveThresh  = 1.5f;
    private float   _lastRePathTime             = -999f;
    private Vector2 _lastClusterPosWhenPathed   = Vector2.positiveInfinity;

    // ── Transfer grace period ─────────────────────────────────────────────────
    // After exit conditions are satisfied we keep holding for this many seconds
    // so nearby followers can complete any in-flight SharePotion transfers.
    private const float TransferGraceDuration = 3.5f;
    private float       _conditionsClearedAt  = -1f;  // -1 = conditions not yet clear

    private const int   NumDirections = 16;

    public LeaderRally(Blackboard bb,
                       float partyHurtThreshold = 0.60f,
                       float enemyScanRange     = 12f,
                       float rallyDistance      = 7f,
                       float holdRange          = 1.5f) : base(bb)
    {
        _partyHurtThreshold = partyHurtThreshold;
        _enemyScanRange     = enemyScanRange;
        _rallyDistance      = rallyDistance;
        _holdRange          = holdRange;
    }

    public override NodeState Evaluate()
    {
        var self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        // ── Leader-only ───────────────────────────────────────────────────────
        if (FormationManager.Instance?.IsLeader(self) != true)
            return NodeState.Failure;

        // ── Collect nearby enemies ─────────────────────────────────────────────
        var enemies      = CollectNearbyEnemies(self.position, _enemyScanRange);
        bool noThreat    = enemies.Count == 0;
        bool partyHealed = !AnyAllyHurt();

        // ── While not yet holding, exit immediately if conditions aren't met ──
        // (We only enter the grace period after the leader has physically arrived.)
        if (!_isHolding)
        {
            if (noThreat || partyHealed)
                return NodeState.Failure;   // nothing to rally for — skip entirely
        }

        // ── Compute enemy cluster centroid (use last known if no enemies now) ──
        Vector2 clusterPos = _lastClusterPosWhenPathed;
        if (enemies.Count > 0)
        {
            clusterPos = Vector2.zero;
            foreach (var e in enemies) clusterPos += (Vector2)e;
            clusterPos /= enemies.Count;
        }

        // ── If already holding, manage the grace period and re-path ──────────
        if (_isHolding)
        {
            bool conditionsMetNow = noThreat || partyHealed;

            if (conditionsMetNow)
            {
                // Start the transfer countdown the first time conditions clear.
                if (_conditionsClearedAt < 0f)
                {
                    _conditionsClearedAt = Time.time;
                    Debug.Log($"[LeaderRally] {self.name} conditions clear — " +
                              $"waiting {TransferGraceDuration:F1}s for item transfers.");
                }

                float elapsed = Time.time - _conditionsClearedAt;
                if (elapsed >= TransferGraceDuration)
                {
                    ExitRally(self);
                    return NodeState.Failure;   // grace period elapsed — leave rally
                }

                // Still within grace period — hold and let followers transfer.
                return NodeState.Running;
            }
            else
            {
                // Conditions deteriorated (new enemy / ally hurt again) — reset countdown.
                if (_conditionsClearedAt >= 0f)
                {
                    _conditionsClearedAt = -1f;
                    Debug.Log($"[LeaderRally] {self.name} conditions returned — resetting transfer timer.");
                }

                // Re-path only if the enemy cluster has shifted significantly.
                float clusterMoved = Vector2.Distance(clusterPos, _lastClusterPosWhenPathed);
                bool  timeToRePath = Time.time - _lastRePathTime >= RePathInterval;
                if (timeToRePath && clusterMoved >= EnemyClusterMoveThresh)
                    RequestRallyPath(self, enemies, clusterPos);

                return NodeState.Running;
            }
        }

        // ── Not yet holding — check arrival ───────────────────────────────────
        if (_rallyMarker != null)
        {
            float dist = Vector2.Distance(self.position, _rallyMarker.position);
            if (dist <= _holdRange)
            {
                _isHolding = true;
                Debug.Log($"[LeaderRally] {self.name} ARRIVED at rally point — holding for party.");
                return NodeState.Running;
            }
        }

        // ── Need a new path ───────────────────────────────────────────────────
        float clusterDelta    = Vector2.Distance(clusterPos, _lastClusterPosWhenPathed);
        bool  pathTimeElapsed = Time.time - _lastRePathTime >= RePathInterval;

        if (pathTimeElapsed || clusterDelta >= EnemyClusterMoveThresh || _rallyMarker == null)
            RequestRallyPath(self, enemies, clusterPos);

        return NodeState.Running;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RequestRallyPath(Transform self,
                                  System.Collections.Generic.List<Vector3> enemies,
                                  Vector2 clusterPos)
    {
        Vector2 selfPos   = self.position;
        Vector2 safestDir = ComputeSafestDirection(selfPos, enemies);

        Vector2 rallyPos = selfPos + safestDir * _rallyDistance;

        // Snap to walkable node
        var grid = GridGenerator.Instance;
        if (grid != null)
        {
            var node = grid.GetNodeAtWorldPosition(rallyPos)
                    ?? grid.GetNearestWalkableNode(rallyPos, maxSearchRadius: 12);
            if (node != null) rallyPos = node.transform.position;
        }

        EnsureMarker(self);
        _rallyMarker.position = rallyPos;
        _lastRePathTime             = Time.time;
        _lastClusterPosWhenPathed   = clusterPos;

        var mc = self.GetComponent<MovementComponent>();
        if (mc != null)
            mc.OnTriggerMove(self, _rallyMarker);

        Debug.Log($"[LeaderRally] {self.name} heading to rally point {rallyPos} " +
                  $"(safest dir {safestDir}, {enemies.Count} enemies)");
    }

    /// <summary>
    /// Samples <see cref="NumDirections"/> evenly-spaced directions and returns the
    /// one that maximises the sum of distances from ALL nearby enemies.
    /// This picks the direction leading to the largest enemy-free space — which may
    /// require passing between or past individual enemies to reach it.
    /// </summary>
    private static Vector2 ComputeSafestDirection(Vector2 selfPos,
        System.Collections.Generic.List<Vector3> enemies)
    {
        Vector2 bestDir   = Vector2.down;
        float   bestScore = float.MinValue;

        for (int i = 0; i < NumDirections; i++)
        {
            float   angle = i * (360f / NumDirections) * Mathf.Deg2Rad;
            Vector2 dir   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // Score = sum of distances from this candidate to every nearby enemy.
            // Higher = further from enemy cluster overall.
            float score = 0f;
            foreach (var ePos in enemies)
                score += Vector2.Distance(selfPos + dir, (Vector2)ePos);

            if (score > bestScore)
            {
                bestScore = score;
                bestDir   = dir;
            }
        }

        return bestDir;
    }

    private System.Collections.Generic.List<Vector3> CollectNearbyEnemies(
        Vector2 origin, float range)
    {
        var result = new System.Collections.Generic.List<Vector3>();
        foreach (var e in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            if (e == null) continue;
            var hp = e.GetComponent<HealthComponent>();
            if (hp != null && hp.currentHealth <= 0f) continue;
            if (Vector2.Distance(origin, e.transform.position) <= range)
                result.Add(e.transform.position);
        }
        return result;
    }

    private bool AnyAllyHurt()
    {
        foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (p == null) continue;
            var hc = p.GetComponent<HealthComponent>();
            if (hc == null) continue;
            if (hc.currentHealth / hc.maxHealth < _partyHurtThreshold)
                return true;
        }
        return false;
    }

    private void ExitRally(Transform self)
    {
        if (_isHolding)
            Debug.Log($"[LeaderRally] {self.name} ending rally — transfer grace period complete.");
        _isHolding                = false;
        _conditionsClearedAt      = -1f;
        _lastClusterPosWhenPathed = Vector2.positiveInfinity;
    }

    private void EnsureMarker(Transform hero)
    {
        if (_rallyMarker != null) return;
        var go = new GameObject("[RallyMarker]")
        {
            hideFlags = UnityEngine.HideFlags.HideInHierarchy | UnityEngine.HideFlags.HideInInspector
        };
        go.transform.SetParent(hero, worldPositionStays: false);
        _rallyMarker = go.transform;
    }
}
