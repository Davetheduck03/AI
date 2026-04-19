using UnityEngine;

/// <summary>
/// ACTION: Finds the NEAREST player (tagged "Player") within detection range
/// that also has a clear line of sight.
///
/// Using FindGameObjectsWithTag (plural) so every hero in a 4-player party is
/// considered — the original singular version always returned the same hero,
/// causing enemies to ignore whichever heroes Unity didn't happen to return first.
///
/// On losing sight the enemy's path is stopped so it doesn't ghost toward the
/// last known position.
/// </summary>
public class FindPlayer : Node
{
    private float detectionRange;
    private LayerMask visionBlockingLayers;

    // Tracks whether we had a target last tick so we stop movement exactly once
    // on the transition from "chasing" → "lost".
    private bool wasChasing = false;

    // ── Scan throttle ────────────────────────────────────────────────────────
    // Without throttling, FindGameObjectsWithTag + LOS raycasts run every BT
    // tick (20 Hz × N enemies).  At the edge of detection range the player can
    // flicker in/out of LOS between ticks, alternating Success→Failure every
    // frame — the enemy oscillates between chasing and picking a new patrol
    // point, producing visible twitching.  A small cache eliminates the flicker:
    // once a player is spotted they remain "spotted" for ScanInterval, giving
    // the chase sequence time to settle.
    private const float ScanInterval  = 0.15f;
    private float       _nextScanTime = float.MinValue;
    private Transform   _cachedTarget = null;

    public FindPlayer(Blackboard bb, float range = 15f, LayerMask? blockingLayers = null) : base(bb)
    {
        detectionRange = range;
        visionBlockingLayers = blockingLayers ?? LayerMask.GetMask("Walls");
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        // Return cached result if still within the scan window and the cached
        // target is still alive and in range (don't hold the cache if the hero
        // has run far out of detection range since the last scan).
        if (Time.time < _nextScanTime && _cachedTarget != null)
        {
            if (_cachedTarget.gameObject != null &&
                Vector3.Distance(self.position, _cachedTarget.position) <= detectionRange * 1.2f)
            {
                wasChasing = true;
                bb.Set("target", _cachedTarget);
                return NodeState.Success;
            }
            _cachedTarget = null;
        }

        _nextScanTime = Time.time + ScanInterval;

        // Check every hero tagged "Player" and pick the nearest one in LOS.
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        Transform bestTarget = null;
        float bestDist = detectionRange;

        foreach (GameObject playerObj in players)
        {
            if (playerObj == null) continue;

            float dist = Vector3.Distance(self.position, playerObj.transform.position);
            if (dist > bestDist) continue;

            if (!VisionUtilities.HasLineOfSight(self.position, playerObj.transform.position, visionBlockingLayers))
                continue;

            bestDist = dist;
            bestTarget = playerObj.transform;
        }

        if (bestTarget != null)
        {
            _cachedTarget = bestTarget;
            wasChasing = true;
            bb.Set("target", bestTarget);
            Debug.Log($"Enemy spotted player at distance: {bestDist:F1}");
            return NodeState.Success;
        }

        _cachedTarget = null;

        // Player left line-of-sight — stop any leftover chase movement so the
        // enemy doesn't ghost toward the last known position.
        if (wasChasing)
        {
            wasChasing = false;
            var pf = self.GetComponent<UnitPathFollower>();
            pf?.StopPath();
            bb.Set<Transform>("target", null);
            Debug.Log($"[FindPlayer] {self.name} lost sight of all players — stopping chase.");
        }

        return NodeState.Failure;
    }
}