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

    public FindPlayer(Blackboard bb, float range = 15f, LayerMask? blockingLayers = null) : base(bb)
    {
        detectionRange = range;
        visionBlockingLayers = blockingLayers ?? LayerMask.GetMask("Walls");
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

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
            wasChasing = true;
            bb.Set("target", bestTarget);
            Debug.Log($"Enemy spotted player at distance: {bestDist:F1}");
            return NodeState.Success;
        }

        // Player left line-of-sight — stop any leftover chase movement so the
        // enemy doesn't ghost toward the last known position.
        if (wasChasing)
        {
            wasChasing = false;
            var pf = self.GetComponent<UnitPathFollower>();
            pf?.StopPath();
            bb.Set<Transform>("target", null);
            Debug.Log($"[FindPlayer] {self.name} lost sight of all players — dropping aggro");
        }

        return NodeState.Failure;
    }
}
