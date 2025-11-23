using UnityEngine;

public class KnightAI : BehaviorTreeRunner
{
    protected override Node BuildTree()
    {
        var rootSelector = new Selector(bb);

        // Attack/Chase branch
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new CanSeeTarget(bb, viewDistance));
        attackSeq.AddChild(new ChaseTarget(bb, chaseSpeed));
        rootSelector.AddChild(attackSeq);

        // Patrol fallback
        rootSelector.AddChild(new Patrol(bb, patrolWaypoints));

        return rootSelector;
    }
}