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
    // Approach distance is now driven by the equipped weapon's range (WeaponSO.range → DamageComponent.AttackRange).

    protected override void Start()
    {
        base.Start();
    }

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // Priority 0: EXTRACT — runs only after the Relic is collected.
        // Overrides combat/loot/explore — hero ignores everything and heads for the exit.
        var extractSeq = new Sequence(bb);
        extractSeq.AddChild(new SetExtractionTarget(bb));           // Fails if no relic
        extractSeq.AddChild(new MoveTowardsTarget(bb, 1.0f));       // Walk to extraction point
        extractSeq.AddChild(new TriggerWin(bb));                    // Fire Win state on arrival
        root.AddChild(extractSeq);

        // Priority 1: ATTACK
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestRevealedEnemy(bb, enemyDetectionRange));
        attackSeq.AddChild(new MoveAndAttack(bb, enemyLayer));
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
		// approachRange matches LootTarget's maxLootDistance so the pathfinder
		// can reliably arrive — PickupItem has no distance requirement of its own.
		var worldItemSeq = new Sequence(bb);
		worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
		worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 8f));
		worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
		worldItemSeq.AddChild(new PickupItem(bb));
		root.AddChild(worldItemSeq);

		// Priority 4: EXPLORE
		var exploreSeq = new Sequence(bb);
        exploreSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
        exploreSeq.AddChild(new FindFogCluster(bb, 50f));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 0.5f)); // must reach the fog tile, not just get close
        root.AddChild(exploreSeq);

        return root;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange);
    }
}