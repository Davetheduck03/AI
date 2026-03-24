// BasicEnemyAI.cs
using UnityEngine;

/// <summary>
/// Enemy AI: Chases and attacks player when visible, patrols when not.
/// </summary>
public class BasicEnemyAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask visionBlockingLayers;

    // Approach distance is driven by EnemySO.range → DamageComponent.AttackRange.
    // Set the range value on the EnemySO asset to control how close enemies get.

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // Priority 1: Chase and attack player
        var chaseSeq = new Sequence(bb);
        chaseSeq.AddChild(new FindPlayer(bb, 15f, visionBlockingLayers));
        chaseSeq.AddChild(new MoveAndAttack(bb, playerLayer));
        root.AddChild(chaseSeq);

        // Priority 2: Patrol
        root.AddChild(new Patrol(bb));

        return root;
    }
}