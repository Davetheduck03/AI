using UnityEngine;

/// <summary>
/// Enemy AI: Chases and attacks player when visible, returns to spawn when not.
/// Enemies use DamageComponent defaults (range=2, isAoE=false) since they have EnemySO.
/// </summary>
public class BasicEnemyAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask visionBlockingLayers;

    [Header("Movement")]
    [SerializeField] private float approachDistance = 1.5f;

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // Priority 1: Chase and attack player
        var chaseSeq = new Sequence(bb);
        chaseSeq.AddChild(new FindPlayer(bb, 15f, visionBlockingLayers));
        chaseSeq.AddChild(new MoveTowardsTarget(bb, approachDistance));
        chaseSeq.AddChild(new IsInAttackRange(bb, approachDistance));
        chaseSeq.AddChild(new AttackTarget(bb, playerLayer));   // range from DamageComponent
        root.AddChild(chaseSeq);

        // Priority 2: Return to spawn
        var returnSeq = new Sequence(bb);
        returnSeq.AddChild(new ReturnToSpawn(bb));
        returnSeq.AddChild(new MoveTowardsTarget(bb, 1f));
        returnSeq.AddChild(new HasReachedTarget(bb, 0.1f));
        root.AddChild(returnSeq);

        // Priority 3: Idle
        root.AddChild(new IdleAtSpawn(bb));

        return root;
    }
}