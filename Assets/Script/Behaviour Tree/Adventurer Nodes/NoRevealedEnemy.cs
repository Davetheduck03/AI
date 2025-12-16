using UnityEngine;

/// <summary>
/// CONDITION: Returns Success if NO revealed enemies are in range.
/// Use as a guard to prevent loot/explore when enemies are visible.
/// This ensures attack sequence gets priority.
/// </summary>
public class NoRevealedEnemies : Node
{
    private float detectionRange;
    private FogOfWarManager fogManager;

    public NoRevealedEnemies(Blackboard bb, float range = 10f) : base(bb)
    {
        detectionRange = range;
        fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            // Skip enemies in fog
            if (fogManager != null && !fogManager.IsRevealed(enemyObj.transform.position))
            {
                continue;
            }

            float dist = Vector3.Distance(self.position, enemyObj.transform.position);

            if (dist <= detectionRange)
            {
                Debug.Log($"[NoRevealedEnemies] Enemy {enemyObj.name} detected at {dist:F1} - blocking non-combat actions");
                return NodeState.Failure;
            }
        }

        return NodeState.Success;
    }
}