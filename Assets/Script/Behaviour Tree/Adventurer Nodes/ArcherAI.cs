using UnityEngine;

/// <summary>
/// Archer AI — Priority: Extract > Attack (adaptive) > Guard relic carrier > Loot > Pick up items > Explore.
///
/// Uses AdaptiveAttack so behaviour automatically matches the equipped weapon:
///   Bow equipped   → kites at preferred range (normal archer behaviour).
///   Melee equipped → charges in like a knight (unusual but handled gracefully).
/// </summary>
public class ArcherAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask wallLayers;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 14f;

    [Header("Kiting (when bow equipped)")]
    [Tooltip("Preferred standoff distance. Clamped to attackRange - 0.3 automatically.")]
    [SerializeField] private float kiteDistance = 3.5f;

    protected override void Start()
    {
        base.Start();
    }

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // ── Priority 0: EXTRACT ──────────────────────────────────────────────
        var extractSeq = new Sequence(bb);
        extractSeq.AddChild(new SetExtractionTarget(bb));
        extractSeq.AddChild(new MoveTowardsTarget(bb, 1.0f));
        extractSeq.AddChild(new TriggerWin(bb));
        root.AddChild(extractSeq);

        // ── Priority 1: ATTACK (adaptive) ────────────────────────────────────
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestRevealedEnemy(bb, enemyDetectionRange, wallLayers));
        attackSeq.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));
        root.AddChild(attackSeq);

        // ── Priority 2: GUARD RELIC CARRIER ─────────────────────────────────
        var guardSeq = new Sequence(bb);
        guardSeq.AddChild(new IsRelicHeldByTeammate(bb, team));
        guardSeq.AddChild(new MoveTowardsTarget(bb, 2f, "relicHolder"));
        root.AddChild(guardSeq);

        // ── Priority 3: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 4: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new Sequence(bb);
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 5: FOLLOW LEADER (followers only) ───────────────────────
        var followSeq = new Sequence(bb);
        followSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        followSeq.AddChild(new FollowLeader(bb, 1.5f));
        root.AddChild(followSeq);

        // ── Priority 6: EXPLORE (leader + fallback for all) ──────────────────
        var exploreSeq = new Sequence(bb);
        exploreSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        exploreSeq.AddChild(new FindFogCluster(bb, 50f));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        root.AddChild(exploreSeq);

        return root;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange);

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, kiteDistance);
    }
}