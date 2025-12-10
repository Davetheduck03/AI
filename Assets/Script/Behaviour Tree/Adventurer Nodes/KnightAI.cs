using UnityEngine;

public class KnightAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // Priority 1: Attack revealed enemies
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestEnemy(bb, 15f));
        attackSeq.AddChild(new IsTargetRevealed(bb));
        attackSeq.AddChild(new MoveTowardsTarget(bb, 1f));
        attackSeq.AddChild(new IsInAttackRange(bb));
        attackSeq.AddChild(new AttackTarget(bb, 5f, 5f, 1f, enemyLayer));
        root.AddChild(attackSeq);

        // Priority 2: Collect revealed loot
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0f));
        root.AddChild(lootSeq);

        // Priority 3: Explore unrevealed areas
        var exploreSeq = new Sequence(bb);
        exploreSeq.AddChild(new FindUnexploredArea(bb, 100f));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 2f));
        root.AddChild(exploreSeq);

        return root;
    }
}