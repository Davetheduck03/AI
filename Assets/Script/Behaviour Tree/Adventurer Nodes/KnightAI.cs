using UnityEngine;

/// <summary>
/// Knight AI with fog cluster-based exploration.
/// Better for open/corridor maps than room detection.
/// Priority: Attack > Loot > Large fog clusters (if healthy) > Any fog
/// </summary>
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
        attackSeq.AddChild(new IsInAttackRange(bb, 5f));
        attackSeq.AddChild(new AttackTarget(bb, 5f, 1f, enemyLayer));
        root.AddChild(attackSeq);

        // Priority 2: Collect revealed loot
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // Priority 3: Explore fog clusters (health-aware)
        var clusterSeq = new Sequence(bb);
        clusterSeq.AddChild(new FindFogCluster(bb, 100f));
        clusterSeq.AddChild(new MoveTowardsTarget(bb, 1f));
        root.AddChild(clusterSeq);

        // Priority 4: Fallback to basic exploration
        var basicExploreSeq = new Sequence(bb);
        basicExploreSeq.AddChild(new FindUnexploredArea(bb, 100f));
        basicExploreSeq.AddChild(new MoveTowardsTarget(bb, 1f));
        root.AddChild(basicExploreSeq);

        return root;
    }
}