using UnityEngine;

/// <summary>
/// ACTION: Finds the nearest REVEALED enemy (not in fog).
/// Always re-checks for enemies (doesn't cache).
/// </summary>
public class FindNearestRevealedEnemy : Node
{
    private float maxDetectionRange;
    private FogOfWarManager fogManager;

    // Optional wall layer used for LOS — passed from the AI MonoBehaviour.
    // If left at default (0) no LOS cast is performed (backwards-compatible).
    private readonly LayerMask _wallLayers;

    // Tracks whether we had a combat target last tick so we stop movement exactly
    // once on the "engaged → no target" transition instead of every frame.
    private bool _wasEngaged = false;

    public FindNearestRevealedEnemy(Blackboard bb, float range = 20f, LayerMask wallLayers = default) : base(bb)
    {
        maxDetectionRange = range;
        fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
        _wallLayers = wallLayers;
    }

    public override NodeState Evaluate()
    {
        // Hero is locked into a loot animation — do not interrupt with combat.
        if (bb.Get<bool>("isLooting"))
            return NodeState.Failure;

        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float closestDist = maxDetectionRange;

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            // Skip enemies still in fog
            if (fogManager != null && !fogManager.IsRevealed(enemyObj.transform.position))
                continue;

            float dist = Vector3.Distance(self.position, enemyObj.transform.position);
            if (dist >= closestDist) continue;

            // Skip enemies behind walls (when a wall layer is configured)
            if (_wallLayers != 0 &&
                !VisionUtilities.HasLineOfSight(self.position, enemyObj.transform.position, _wallLayers))
                continue;

            closestDist = dist;
            nearest = enemyObj.transform;
        }

        if (nearest != null)
        {
            // Hero is switching to combat — release any chest claim so other heroes
            // can loot it instead of waiting indefinitely.
            Transform currentTarget = bb.Get<Transform>("target");
            if (currentTarget != null)
            {
                Lootable lootable = currentTarget.GetComponent<Lootable>();
                if (lootable != null)
                    lootable.ReleaseClaim(self);
            }

            _wasEngaged = true;
            bb.Set("target", nearest);

            // Broadcast the leader's active combat target to the team so followers
            // can assist via AssistLeaderInCombat even if the enemy is outside their
            // own detection range.
            if (FormationManager.Instance?.IsLeader(self) == true)
                TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", nearest);

            Debug.Log($"[FindNearestRevealedEnemy] Targeting {nearest.name} at distance {closestDist:F1}");
            return NodeState.Success;
        }

        // Enemy left LOS — stop any leftover kite/charge movement on the transition
        // frame so the hero doesn't coast toward the enemy's last known position.
        if (_wasEngaged)
        {
            _wasEngaged = false;

            // Clear the team broadcast if this hero was the leader.
            if (FormationManager.Instance?.IsLeader(self) == true)
                TeamBlackboard.Instance?.Set<Transform>("leaderCombatTarget", null);

            var pf = self.GetComponent<UnitPathFollower>();
            pf?.StopPath();
            Debug.Log($"[FindNearestRevealedEnemy] {self.name} lost all enemies — stopping movement");
        }

        return NodeState.Failure;
    }
}