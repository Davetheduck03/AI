using UnityEngine;

/// <summary>
/// Unified combat target selector — replaces the dual-sequence pattern of
/// FindNearestRevealedEnemy (Priority 1) + AssistLeaderInCombat (lower priority).
///
/// Having two separate attack sequences with separate KiteAndAttack instances caused
/// the ranged heroes to thrash: the two instances alternated calling TriggerMove to
/// slightly different positions every 0.5 s, constantly restarting the path coroutine
/// and preventing them from ever settling into a stable firing position.
///
/// This node consolidates into a single sequence so there is exactly one
/// KiteAndAttack instance per hero at all times.
///
/// TEAM BROADCAST
/// ──────────────
/// Any hero that finds an enemy writes it to TeamBlackboard["leaderCombatTarget"]
/// so the whole party can rally, even when the leader is in a different room.
/// The leader's own scan has the highest authority — it overwrites whatever a
/// follower may have set.  Followers only write when the current team target is
/// absent or dead (first-finder semantics).  This prevents multiple followers
/// from thrashing the shared key with different enemies simultaneously.
///
/// TARGET PRIORITY (per hero)
/// ───────────────────────────
/// 1. SELF-DEFENCE — enemy within selfDefenseRange (works for all heroes).
///    These are close-range threats that override the team target locally.
///    A close threat is also broadcast if no team target exists yet.
/// 2. TEAM TARGET  — TeamBlackboard["leaderCombatTarget"].
///    Set by any engaged hero; the leader's write is authoritative.
/// 3. OWN SCAN     — nearest revealed enemy within detection range + LOS.
///    Fallback when neither the team board nor close threats are present.
///    Result is broadcast as the new team target.
///
/// THROTTLING
/// ──────────
/// FindGameObjectsWithTag + LOS raycasts are expensive.  The full scan runs at
/// most every ScanInterval seconds; between scans the cached result is returned.
/// The cache is invalidated immediately if the target dies.
/// </summary>
public class SelectCombatTarget : Node
{
    private readonly float     _selfDefenseRange;
    private readonly float     _detectionRange;
    private readonly LayerMask _wallLayers;

    // Throttle: avoid calling FindGameObjectsWithTag + HasLineOfSight every frame.
    private const float ScanInterval  = 0.2f;
    private float       _nextScanTime = float.MinValue;
    private Transform   _cachedTarget = null;

    private FogOfWarManager _fogManager;

    // Tracks engagement state so we stop movement exactly once on the
    // "engaged → no target" transition frame instead of every frame.
    private bool _wasEngaged = false;

    public SelectCombatTarget(
        Blackboard bb,
        float      selfDefenseRange = 3.0f,
        float      detectionRange   = 14f,
        LayerMask  wallLayers       = default) : base(bb)
    {
        _selfDefenseRange = selfDefenseRange;
        _detectionRange   = detectionRange;
        _wallLayers       = wallLayers;
        _fogManager       = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        // Hero is locked into a loot animation — do not interrupt with combat.
        if (bb.Get<bool>("isLooting"))
            return NodeState.Failure;

        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        bool isLeader = FormationManager.Instance?.IsLeader(self) == true;

        // ── Return cached result if scan interval hasn't elapsed ─────────────
        // Avoids per-frame FindGameObjectsWithTag + raycasts.
        if (Time.time < _nextScanTime && _cachedTarget != null)
        {
            if (_cachedTarget.gameObject != null)
            {
                var hpCheck = _cachedTarget.GetComponent<HealthComponent>();
                if (hpCheck == null || hpCheck.currentHealth > 0)
                {
                    bb.Set("target", _cachedTarget);
                    _wasEngaged = true;
                    // Leader always refreshes the broadcast so it stays authoritative.
                    if (isLeader)
                        TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", _cachedTarget);
                    return NodeState.Success;
                }
            }
            // Cached target died or was destroyed — fall through to immediate rescan.
            _cachedTarget = null;
        }

        _nextScanTime = Time.time + ScanInterval;

        Transform selected = null;

        // ── Target selection ─────────────────────────────────────────────────

        // 1. Self-defence: closest enemy within personal threat range.
        //    Applies to ALL heroes (leader and followers alike).
        selected = FindNearest(self, _selfDefenseRange);

        // 2. Team target: rally to whatever any engaged hero is fighting.
        if (selected == null)
        {
            Transform teamTarget =
                TeamBlackboard.Instance?.Get<Transform>("leaderCombatTarget");

            if (teamTarget != null && teamTarget.gameObject != null)
            {
                var hp = teamTarget.GetComponent<HealthComponent>();
                bool alive    = hp == null || hp.currentHealth > 0;
                // CRITICAL: also verify the target is currently revealed.
                // A follower may have flagged an enemy that has since walked back
                // into unexplored fog.  Without this check the leader would spend
                // up to ClosingTimeout (seconds) chasing an invisible target and
                // never fall through to exploration.
                bool revealed = _fogManager == null ||
                                _fogManager.IsRevealed(teamTarget.position);

                if (alive && revealed)
                {
                    selected = teamTarget;
                }
                else
                {
                    // Stale — dead or back in fog.  Clear immediately so the whole
                    // team stops trying to engage it.
                    TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", null);
                }
            }
        }

        // 3. Own scan: nearest revealed enemy within detection range + LOS.
        if (selected == null)
            selected = FindNearest(self, _detectionRange);

        // ── Broadcast ────────────────────────────────────────────────────────
        // The leader always writes (authoritative).
        // Followers write only when the current team target is absent or dead
        // (first-finder semantics — avoids multiple followers thrashing the key).
        if (selected != null)
        {
            if (isLeader)
            {
                TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", selected);
            }
            else
            {
                Transform currentTeam =
                    TeamBlackboard.Instance?.Get<Transform>("leaderCombatTarget");
                bool teamNeedsTarget = currentTeam == null
                    || currentTeam.gameObject == null
                    || (currentTeam.GetComponent<HealthComponent>()?.currentHealth <= 0);
                if (teamNeedsTarget)
                    TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", selected);
            }
        }
        else if (isLeader)
        {
            // Leader lost all enemies — clear the team broadcast so followers
            // stop chasing and return to formation.
            TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", null);
        }

        // ── Target acquired ──────────────────────────────────────────────────
        if (selected != null)
        {
            // Release any chest claim so other heroes can loot it instead.
            Transform currentTarget = bb.Get<Transform>("target");
            if (currentTarget != null && currentTarget != selected)
            {
                var lootable = currentTarget.GetComponent<Lootable>();
                if (lootable != null) lootable.ReleaseClaim(self);
            }

            _cachedTarget = selected;
            _wasEngaged   = true;
            bb.Set("target", selected);
            return NodeState.Success;
        }

        // ── No target found ──────────────────────────────────────────────────
        _cachedTarget = null;
        if (_wasEngaged)
        {
            _wasEngaged = false;
            // Stop any leftover kite/charge movement so the hero doesn't coast
            // toward the enemy's last known position after combat ends.
            self.GetComponent<UnitPathFollower>()?.StopPath();
        }

        return NodeState.Failure;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the nearest revealed, living enemy within <paramref name="range"/>
    /// that has line-of-sight (when wallLayers is non-zero), or null if none.
    /// </summary>
    private Transform FindNearest(Transform self, float range)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform    nearest = null;
        float closestDist    = range;

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            // Skip dead enemies — they may still have the "Enemy" tag during a
            // death animation before the GameObject is destroyed.
            var hp = enemyObj.GetComponent<HealthComponent>();
            if (hp != null && hp.currentHealth <= 0) continue;

            if (_fogManager != null &&
                !_fogManager.IsRevealed(enemyObj.transform.position))
                continue;

            float dist = Vector3.Distance(self.position, enemyObj.transform.position);
            if (dist >= closestDist) continue;

            if (_wallLayers != 0 &&
                !VisionUtilities.HasLineOfSight(
                    self.position, enemyObj.transform.position, _wallLayers))
                continue;

            closestDist = dist;
            nearest     = enemyObj.transform;
        }

        return nearest;
    }
}
