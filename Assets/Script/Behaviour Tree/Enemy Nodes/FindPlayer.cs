using UnityEngine;

/// <summary>
/// ACTION: Finds the player (tagged "Player") within detection range.
/// Returns Success if found, Failure if out of range or not visible.
/// </summary>
public class FindPlayer : Node
{
    private float detectionRange;
    private LayerMask visionBlockingLayers;

    public FindPlayer(Blackboard bb, float range = 15f, LayerMask? blockingLayers = null) : base(bb)
    {
        detectionRange = range;
        visionBlockingLayers = blockingLayers ?? LayerMask.GetMask("Walls");
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player == null) return NodeState.Failure;

        float dist = Vector3.Distance(self.position, player.transform.position);

        // Check if in range
        if (dist > detectionRange)
        {
            return NodeState.Failure;
        }

        // Check line of sight
        if (!HasLineOfSight(self.position, player.transform.position))
        {
            return NodeState.Failure;
        }

        // Player found and visible
        bb.Set("target", player.transform);
        Debug.Log($"Enemy spotted player at distance: {dist:F1}");
        return NodeState.Success;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        Vector2 direction = (to - from).normalized;
        float distance = Vector2.Distance(from, to);
        RaycastHit2D hit = Physics2D.Raycast(from, direction, distance, visionBlockingLayers);
        return hit.collider == null;
    }
}
