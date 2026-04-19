using UnityEngine;

/// <summary>
/// Paladin AI — Priority: Extract > Heal critical ally > Attack > Heal injured ally > Loot > Items > Follow > Explore.
///
/// The paladin is a hybrid damage/support unit.
///
///   HEALING requires a Staff. Without a staff the heal sequences are skipped and
///   the paladin acts as a pure melee/ranged damage dealer.
///
///   CRITICAL heal (priority 1, no enemy guard, staff required)
///     Fires even during a fight. The paladin breaks off combat if a teammate
///     drops below <see cref="criticalHPThreshold"/> (default 40 %).
///
///   NORMAL heal (priority 3, enemy guard, staff required)
///     Only fires when the area is clear of enemies.
///
/// Combat uses AdaptiveAttack — sword charges in, staff/bow kites.
/// </summary>
public class PaladinAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask wallLayers;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 12f;

    [Header("Combat")]
    [Tooltip("Preferred standoff range when a ranged weapon is equipped.")]
    [SerializeField] private float kiteDistance = 3f;

    [Header("Healing")]
    [Tooltip("Allies at or below this HP fraction interrupt combat — the paladin heals them immediately.")]
    [SerializeField] private float criticalHPThreshold = 0.4f;

    [Tooltip("Allies at or below this HP fraction are healed when no enemies are nearby.")]
    [SerializeField] private float healThreshold = 0.75f;

    [Tooltip("World-unit radius the paladin scans for injured allies.")]
    [SerializeField] private float healSearchRange = 12f;

    [Tooltip("World-unit radius the paladin must be within before casting a heal.")]
    [SerializeField] private float healRange = 2.5f;

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

        // ── Priority 1: HEAL CRITICAL ALLY (staff required) ──────────────────
        // No enemy guard — the paladin stops fighting to save a dying teammate.
        var healCritSeq = new Sequence(bb);
        healCritSeq.AddChild(new HasWeaponType(bb, WeaponType.Staff));
        healCritSeq.AddChild(new FindInjuredAlly(bb, criticalHPThreshold, healSearchRange, "healTarget", includeSelf: true));
        healCritSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));
        root.AddChild(healCritSeq);

        // ── Priority 2: ATTACK ───────────────────────────────────────────────
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                  detectionRange: enemyDetectionRange,
                                                  wallLayers: wallLayers));
        attackSeq.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));
        root.AddChild(attackSeq);

        // ── Priority 3: HEAL INJURED ALLY (non-combat, staff required) ───────
        // Heals moderate injuries only when the fight is over.
        var healSeq = new Sequence(bb);
        healSeq.AddChild(new HasWeaponType(bb, WeaponType.Staff));
        healSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        healSeq.AddChild(new FindInjuredAlly(bb, healThreshold, healSearchRange, "healTarget", includeSelf: true));
        healSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));
        root.AddChild(healSeq);

        // ── Priority 4: LOOT CHESTS ──────────────────────────────────────────
        // IsLeaderOrNearLeader gates followers: only loot when within 7 u of
        // the leader so the paladin doesn't abandon the group for a distant chest.
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new IsLeaderOrNearLeader(bb));
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 5: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new Sequence(bb);
        worldItemSeq.AddChild(new IsLeaderOrNearLeader(bb));
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 6: FOLLOW LEADER (followers only) ───────────────────────
        var followSeq = new Sequence(bb);
        followSeq.AddChild(new FollowLeader(bb));
        root.AddChild(followSeq);

        // ── Priority 7: EXPLORE (leader + fallback for all) ──────────────────
        var exploreSeq = new Sequence(bb);
        exploreSeq.AddChild(new FindFogCluster(bb));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        root.AddChild(exploreSeq);

        return root;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Enemy detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange);
    }
}