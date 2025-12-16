using UnityEngine;

/// <summary>
/// Knight AI with reactive combat priority.
/// Immediately interrupts any action when revealed enemy is detected.
/// Priority: Attack > Loot > Explore
/// </summary>
public class KnightAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;

    [Header("Detection Settings")]
    [SerializeField] private float enemyDetectionRange = 10f;
    [SerializeField] private float attackRange = 2f;

    private FogOfWarManager fogManager;

    protected override void Start()
    {
        base.Start();
        fogManager = FindAnyObjectByType<FogOfWarManager>();
    }

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // ============================================
        // Priority 1: ATTACK (Reactive - checks every frame)
        // ============================================
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestRevealedEnemy(bb, enemyDetectionRange));
        attackSeq.AddChild(new MoveTowardsTarget(bb, attackRange));
        attackSeq.AddChild(new AttackTarget(bb, attackRange, 1f, enemyLayer));
        root.AddChild(attackSeq);

        // ============================================
        // Priority 2: LOOT
        // ============================================
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ============================================
        // Priority 3: EXPLORE
        // ============================================
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
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}