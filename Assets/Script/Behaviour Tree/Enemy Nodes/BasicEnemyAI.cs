// BasicEnemyAI.cs
using UnityEngine;

/// <summary>
/// Enemy AI: Chases and attacks player when visible, patrols when not.
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
        chaseSeq.AddChild(new MoveAndAttack(bb, approachDistance, playerLayer));
        root.AddChild(chaseSeq);

        // Priority 2: Patrol
        root.AddChild(new Patrol(bb));

        return root;
    }
}