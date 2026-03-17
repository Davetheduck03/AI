// KnightAI.cs
using UnityEngine;

/// <summary>
/// Knight AI — Priority: Attack > Loot > Explore.
/// </summary>
public class KnightAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 10f;

    [Header("Movement")]
    [Tooltip("How close to get before attempting to attack.")]
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
        attackSeq.AddChild(new MoveAndAttack(bb, approachDistance, enemyLayer));
        root.AddChild(attackSeq);

		// Priority 2: LOOT CHESTS
		var lootSeq = new Sequence(bb);
		lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
		lootSeq.AddChild(new FindLootInRange(bb, 10f));
		lootSeq.AddChild(new IsTargetRevealed(bb));
		lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
		lootSeq.AddChild(new LootTarget(bb));
		root.AddChild(lootSeq);

		// Priority 3: PICK UP WORLD ITEMS (dropped gear)
		var worldItemSeq = new Sequence(bb);
		worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
		worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 4f));
		worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.1f));
		worldItemSeq.AddChild(new PickupItem(bb));
		root.AddChild(worldItemSeq);

		// Priority 4: EXPLORE
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