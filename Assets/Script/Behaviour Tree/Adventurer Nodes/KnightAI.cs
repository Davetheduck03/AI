using UnityEngine;

/// <summary>
/// Knight AI — Priority: Attack > Loot > Explore.
/// attackRange is now owned by DamageComponent (from HeroSO.range).
/// approachDistance controls how close to get before attacking.
/// </summary>
public class KnightAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 10f;

    [Header("Movement")]
    [Tooltip("How close to get before attempting to attack. " +
             "Should be <= HeroSO.range for melee, can equal range for ranged/AoE.")]
    [SerializeField] private float approachDistance = 1f;

    protected override void Start()
    {
        base.Start();
    }

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // Priority 1: ATTACK
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestRevealedEnemy(bb, enemyDetectionRange));
        attackSeq.AddChild(new MoveTowardsTarget(bb, approachDistance));
        attackSeq.AddChild(new AttackTarget(bb, enemyLayer));   // range from DamageComponent
        root.AddChild(attackSeq);

        // Priority 2: LOOT
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // Priority 3: EXPLORE
        var exploreSeq = new Sequence(bb);
        exploreSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
        exploreSeq.AddChild(new FindFogCluster(bb, 50f));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 2f));
        root.AddChild(exploreSeq);

        return root;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, approachDistance);
    }
}