// KnightAI.cs
using UnityEngine;

/// <summary>
/// Knight AI — Priority: Extract > Attack > Loot > Pick up items > Explore.
///
/// Combat adapts to the equipped weapon at runtime:
///   Melee weapon (Sword, LongSword, Dagger) → charges in and attacks.
///   Ranged weapon (Bow)                     → kites at preferred range.
/// </summary>
public class KnightAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask wallLayers;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 10f;

    [Header("Kiting (when ranged weapon equipped)")]
    [Tooltip("Preferred standoff distance used when a bow is equipped.")]
    [SerializeField] private float kiteDistance = 3.5f;

    protected override void Start()
    {
        base.Start();
    }

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // ── Priority 0: EXTRACT ──────────────────────────────────────────────
        var extractSeq = new LabeledSequence(bb, "0: Extract");
        extractSeq.AddChild(new SetExtractionTarget(bb));
        extractSeq.AddChild(new MoveTowardsTarget(bb, 1.0f));
        extractSeq.AddChild(new TriggerWin(bb));
        root.AddChild(extractSeq);

        // ── Priority 1: ATTACK ───────────────────────────────────────────────
        var attackSeq = new LabeledSequence(bb, "1: Attack");
        attackSeq.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                  detectionRange: enemyDetectionRange,
                                                  wallLayers: wallLayers));
        attackSeq.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));
        root.AddChild(attackSeq);

        // ── Priority 2: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new LabeledSequence(bb, "2: Loot");
        lootSeq.AddChild(new IsLeaderOrNearLeader(bb));
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 3: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new LabeledSequence(bb, "3: Items");
        worldItemSeq.AddChild(new IsLeaderOrNearLeader(bb));
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 4: YIELD ITEM SPACE ────────────────────────────────────
        var yieldSeq = new LabeledSequence(bb, "4: Yield Space");
        yieldSeq.AddChild(new YieldItemSpace(bb));
        root.AddChild(yieldSeq);

        // ── Priority 5: FOLLOW LEADER (followers only) ───────────────────────
        var followSeq = new LabeledSequence(bb, "5: Follow");
        followSeq.AddChild(new FollowLeader(bb));
        root.AddChild(followSeq);

        // ── Priority 6: WAIT FOR PARTY UPGRADES (leader only) ───────────────
        var waitSeq = new LabeledSequence(bb, "6: Wait Upgrades");
        waitSeq.AddChild(new WaitForPartyUpgrades(bb));
        root.AddChild(waitSeq);

        // ── Priority 7: EXPLORE (leader + fallback for all) ──────────────────
        var exploreSeq = new LabeledSequence(bb, "7: Explore");
        exploreSeq.AddChild(new FindFogCluster(bb));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
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
