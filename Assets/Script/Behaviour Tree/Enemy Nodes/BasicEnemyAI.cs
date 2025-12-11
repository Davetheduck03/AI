using UnityEngine;

/// <summary>
/// Enemy AI: Chases and attacks player when visible, returns to spawn when not.
/// Does NOT use fog of war - can see through fog.
/// </summary>
public class BasicEnemyAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask visionBlockingLayers;

    protected override Node BuildTree()
    {
        // Root selector: Try behaviors in priority order
        var root = new Selector(bb);

        // PRIORITY 1: Chase and attack player when visible
        var chaseSeq = new Sequence(bb);
        chaseSeq.AddChild(new FindPlayer(bb, 15f, visionBlockingLayers));
        chaseSeq.AddChild(new MoveTowardsTarget(bb, 3f));
        chaseSeq.AddChild(new IsInAttackRange(bb, 5f));
        chaseSeq.AddChild(new AttackTarget(bb, 5, 1, playerLayer));
        root.AddChild(chaseSeq);

        // PRIORITY 2: Return to spawn when player not visible
        var returnSeq = new Sequence(bb);
        returnSeq.AddChild(new ReturnToSpawn(bb));
        returnSeq.AddChild(new MoveTowardsTarget(bb, 1f));
        returnSeq.AddChild(new HasReachedTarget(bb, 0.1f));
        root.AddChild(returnSeq);

        // PRIORITY 3: Idle at spawn
        root.AddChild(new IdleAtSpawn(bb));

        return root;
    }

    //// Debug visualization
    //private void OnDrawGizmosSelected()
    //{
    //    // Detection range
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawWireSphere(transform.position, detectionRange);

    //    // Attack range
    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawWireSphere(transform.position, attackRange);

    //    // Line to spawn position
    //    if (Application.isPlaying && blackboard != null)
    //    {
    //        Vector3? spawnPos = blackboard.Get<Vector3?>("spawnPosition");
    //        if (spawnPos.HasValue)
    //        {
    //            Gizmos.color = Color.cyan;
    //            Gizmos.DrawLine(transform.position, spawnPos.Value);
    //            Gizmos.DrawWireSphere(spawnPos.Value, 1f);
    //        }
    //    }
    //}
}