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
        var extractSeq = new Sequence(bb);
        extractSeq.AddChild(new SetExtractionTarget(bb));
        extractSeq.AddChild(new MoveTowardsTarget(bb, 1.0f));
        extractSeq.AddChild(new TriggerWin(bb));
        root.AddChild(extractSeq);

        // ── Priority 1: ATTACK ───────────────────────────────────────────────
        // AdaptiveAttack checks the equipped weapon every tick and delegates to
        // KiteAndAttack (bow) or MoveAndAttack (melee) automatically.
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestRevealedEnemy(bb, enemyDetectionRange, wallLayers));
        attackSeq.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));
        root.AddChild(attackSeq);

        // ── Priority 2: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 3: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new Sequence(bb);
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 4: FOLLOW LEADER (followers only) ───────────────────────
        // Leaders return Failure from FollowLeader and fall through to Explore.
        // If the leader dies, followers also fall through to Explore (independent).
        var followSeq = new Sequence(bb);
        followSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        followSeq.AddChild(new FollowLeader(bb, 1.5f));
        root.AddChild(followSeq);

        // ── Priority 5: EXPLORE (leader + fallback for all) ──────────────────
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
    }
}