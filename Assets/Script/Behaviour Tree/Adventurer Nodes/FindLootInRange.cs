using UnityEngine;

public class FindLootInRange : Node
{
    private float maxDetectionRange;
    private FogOfWarManager _fogManager;

    // ── Scan throttle ─────────────────────────────────────────────────────────
    // FindGameObjectsWithTag allocates a new array every call.  Running it at
    // 20 Hz × 4 heroes = 80 allocations/s causes GC pressure and frame hitches
    // that look like twitches.  Cache the result; only rescan when the interval
    // elapses OR the cached target has been looted / destroyed / fogged.
    private const float ScanInterval  = 0.4f;
    private float       _nextScanTime = float.MinValue;
    private Transform   _cachedTarget = null;

    public FindLootInRange(Blackboard blackboard, float maxDetectionRange) : base(blackboard)
    {
        this.maxDetectionRange = maxDetectionRange;
        _fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var fogManager = _fogManager;

        // Return cached result if still valid.
        if (Time.time < _nextScanTime && _cachedTarget != null)
        {
            if (_cachedTarget.gameObject != null)
            {
                var loot = _cachedTarget.GetComponent<Lootable>();

                // Invalidate if the chest fell back into fog since we cached it.
                bool stillRevealed = fogManager == null || fogManager.IsRevealed(_cachedTarget.position);

                if (loot != null && !loot.isLooted && stillRevealed)
                {
                    bb.Set("target", _cachedTarget);
                    return NodeState.Success;
                }
            }
            // Cached target was looted, destroyed, or re-fogged — fall through to rescan.
            _cachedTarget = null;
        }

        _nextScanTime = Time.time + ScanInterval;

        GameObject[] lootables = GameObject.FindGameObjectsWithTag("Lootable");
        Transform nearest = null;
        float closestDist = maxDetectionRange;

        foreach (GameObject lootableObj in lootables)
        {
            if (lootableObj == null) continue;

            Lootable lootable = lootableObj.GetComponent<Lootable>();
            if (lootable == null) continue;

            // Skip chests that are fully looted
            if (lootable.isLooted) continue;

            // ── FOG CHECK ────────────────────────────────────────────────────
            // Never target a chest the party hasn't revealed yet.  Without this,
            // FindLootInRange writes the chest to bb["target"] and then
            // IsTargetRevealed immediately fails the loot Sequence — but
            // bb["target"] is now pointing at the chest instead of the fog
            // cluster.  MoveTowardsTarget sees a new target every tick and
            // continuously retriggers A*, causing the hero to twitch in place.
            if (fogManager != null && !fogManager.IsRevealed(lootableObj.transform.position))
                continue;

            // Skip chests claimed by a different alive hero
            if (lootable.claimedBy != null &&
                lootable.claimedBy != self &&
                lootable.claimedBy.gameObject != null &&
                lootable.claimedBy.gameObject.activeInHierarchy)
                continue;

            float dist = Vector3.Distance(self.position, lootableObj.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                nearest = lootableObj.transform;
            }
        }

        if (nearest != null)
        {
            // Claim it so other heroes route to a different chest
            nearest.GetComponent<Lootable>()?.TryClaim(self);
            _cachedTarget = nearest;
            bb.Set("target", nearest);
            return NodeState.Success;
        }

        _cachedTarget = null;
        return NodeState.Failure;
    }
}
