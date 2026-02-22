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

        // Priority 1: Chase and attack
        var chaseSeq = new Sequence(bb);
        chaseSeq.AddChild(new FindPlayer(bb, 15f, visionBlockingLayers));
        chaseSeq.AddChild(new MoveTowardsTarget(bb, approachDistance));
        chaseSeq.AddChild(new AttackTarget(bb, playerLayer));
        root.AddChild(chaseSeq);

        // Priority 2: Patrol
        root.AddChild(new Patrol(bb));

        return root;
    }
}