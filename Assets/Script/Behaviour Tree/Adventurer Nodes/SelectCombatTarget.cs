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

    // ── Disengage grace period ────────────────────────────────────────────────
    // When the FOV cone edge wobbles across an enemy (hero curving around a corner,
    // SeparationBehavior nudging the view direction by a few degrees), the scan
    // alternates hit/miss at 5 Hz (one flip per ScanInterval).  Without a grace
    // period, the Selector sees Success → Failure → Success → Failure on the attack
    // sequence, which means the explore sequence is repeatedly preempted and
    // re-entered every 0.2 s, causing visible stop/start twitching.
    //
    // Fix: when the scan misses but we were recently engaged AND the cached enemy
    // is still alive, continue returning Success for up to MaxMissesBeforeDisengage
    // more scan intervals before truly giving up.  The hero stays in combat mode
    // while SeparationBehavior or path-following briefly rotates the view away from
    // the boundary enemy, rather than yanking back to exploration every 0.2 s.
    private int        _consecutiveMisses       = 0;
    private const int  MaxMissesBeforeDisengage = 2;  // 2 × 0.2s = 0.4s grace window

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

    // ── Team-engagement override ──────────────────────────────────────────────
    /// <summary>
    /// Returns the team's current combat target if it is still alive,
    /// or null if the team is not engaged.  Used both to override the
    /// <c>isLooting</c> guard (so followers rally even mid-chest-animation) and
    /// as the step-2 team-target lookup during the main scan.
    ///
    /// Deliberately does NOT check fog-of-war: the broadcast is set by a hero
    /// that can personally see the enemy, so any follower should trust it
    /// regardless of whether they have personally explored that tile.
    /// </summary>
    private Transform GetLiveTeamTarget()
    {
        Transform tt = TeamBlackboard.Instance?.Get<Transform>("leaderCombatTarget");
        if (tt == null || tt.gameObject == null) return null;

        var hp     = tt.GetComponent<HealthComponent>();
        bool alive = hp == null || hp.currentHealth > 0;

        // Do NOT require the enemy to be personally revealed by this hero.
        // The broadcast was written by a hero that CAN see the enemy — trust it.
        // A fog check here causes distant followers (who haven't yet explored the
        // combat room) to always see the target as "unrevealed" and never rally,
        // even though the FogOfWarManager is shared and the tile IS globally revealed.
        if (alive) return tt;

        // Stale entry — clear it so the whole team stops chasing.
        TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", null);
        return null;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        bool isLeader = FormationManager.Instance?.IsLeader(self) == true;

        // ── Rally override: check for a live team target BEFORE isLooting ────
        // When the leader (or any hero) is engaged, all heroes must respond
        // immediately regardless of what they were doing.  The isLooting guard
        // below only blocks when the party is NOT fighting.
        Transform earlyTeamTarget = null;
        if (!isLeader)
            earlyTeamTarget = GetLiveTeamTarget();

        // Hero is locked into a loot animation — do not interrupt, UNLESS the
        // team is actively engaged and needs help right now.
        if (bb.Get<bool>("isLooting") && earlyTeamTarget == null)
            return NodeState.Failure;

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
        //    For followers we already fetched and validated this above (earlyTeamTarget)
        //    so reuse it.  For leaders, do the lookup fresh here.
        if (selected == null)
        {
            Transform teamTarget = isLeader
                ? GetLiveTeamTarget()           // leader: fresh lookup
                : earlyTeamTarget;              // follower: already validated above

            if (teamTarget != null)
                selected = teamTarget;
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

            _cachedTarget      = selected;
            _wasEngaged        = true;
            _consecutiveMisses = 0;   // reset grace counter — target is confirmed visible
            bb.Set("target", selected);
            return NodeState.Success;
        }

        // ── No target found ──────────────────────────────────────────────────
        // Grace period: if we were just engaged and the cached enemy is still alive,
        // keep returning Success for up to MaxMissesBeforeDisengage more intervals
        // before truly disengaging.  This prevents FOV-boundary flicker (enemy at
        // the edge of the hero's vision cone) from causing rapid combat ↔ explore
        // oscillation every 0.2 s.
        if (_wasEngaged && _cachedTarget != null && _cachedTarget.gameObject != null)
        {
            var graceHp = _cachedTarget.GetComponent<HealthComponent>();
            bool stillAlive = graceHp == null || graceHp.currentHealth > 0;

            if (stillAlive && _consecutiveMisses < MaxMissesBeforeDisengage)
            {
                _consecutiveMisses++;
                bb.Set("target", _cachedTarget);
                return NodeState.Success;   // stay in combat for one more scan
            }
        }

        // Truly no target — clear everything and disengage.
        _cachedTarget      = null;
        _consecutiveMisses = 0;

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
