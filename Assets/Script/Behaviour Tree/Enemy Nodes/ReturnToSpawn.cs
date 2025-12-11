using UnityEngine;

/// <summary>
/// ACTION: Sets the spawn position as the target.
/// Used to make enemies return to their original position.
/// Returns Success always (spawn position is always valid).
/// </summary>
public class ReturnToSpawn : Node
{
    public ReturnToSpawn(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        Vector3? spawnPosition = bb.Get<Vector3?>("spawnPosition");
        
        if (spawnPosition.HasValue)
        {
            // Create target at spawn position
            GameObject targetObj = new GameObject("SpawnTarget");
            targetObj.transform.position = spawnPosition.Value;
            bb.Set("target", targetObj.transform);
            
            Debug.Log("Enemy returning to spawn position");
            return NodeState.Success;
        }
        
        Debug.LogWarning("No spawn position saved!");
        return NodeState.Failure;
    }
}
