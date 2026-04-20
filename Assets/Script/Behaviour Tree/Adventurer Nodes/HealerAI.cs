using UnityEngine;

/// <summary>
/// Healer AI — Priority: Extract > Heal critical ally > Heal injured ally > Loot > Items > Follow leader > Explore.
///
/// The healer never attacks enemies. Its sole contribution in combat is keeping the party
/// alive. Two healing tiers give finer control:
///
///   CRITICAL heal (no enemy guard) — fires even mid-combat when an ally is near death.
///   This lets the healer push through a fight to save a dying teammate.
///
///   NORMAL heal (no enemy guard) — fires whenever any ally needs a top-up.
///   Gating on NoRevealedEnemies meant the healer did nothing during a fight until
///   someone was nearly dead (critical threshold), so the gate was removed.
///
/// When nobody needs healing the healer shadows the leader via FollowLeader so it stays
/// in range to react quickly when someone gets hurt.
/// </summary>
public class HealerAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask wallLayers;

    [Header("Healing")]
    [Tooltip("Allies at or below this HP fraction are treated as critical — healer pushes through combat to reach them.")]
    [SerializeField] private float criticalHPThreshold = 0.35f;

    [Tooltip("Allies at or below this HP fraction receive a normal (out-of-combat) heal.")]
    [SerializeField] private float healThreshold = 0.75f;

    [Tooltip("World-unit radius within which the healer scans for injured allies.")]
    [SerializeField] private float healSearchRange = 14f;

    [Tooltip("World-unit radius the healer must be within before casting.")]
    [SerializeField] private float healRange = 4.0f;

    [Header("Self-preservation")]
    [Tooltip("Enemy detection radius used for the enemy guards on normal healing / follow / explore.")]
    [SerializeField] private float dangerRange = 8f;

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

        // ── Priority 1: HEAL CRITICAL ALLY ───────────────────────────────────
        // No NoRevealedEnemies guard — the healer will brave a fight to save a dying ally.
        var healCritSeq = new LabeledSequence(bb, "1: Heal Critical");
        healCritSeq.AddChild(new FindInjuredAlly(bb, criticalHPThreshold, healSearchRange, "healTarget"));
        healCritSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));
        root.AddChild(healCritSeq);

        // ── Priority 2: HEAL INJURED ALLY ────────────────────────────────────
        var healSeq = new LabeledSequence(bb, "2: Heal Normal");
        healSeq.AddChild(new FindInjuredAlly(bb, healThreshold, healSearchRange, "healTarget"));
        healSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));
        root.AddChild(healSeq);

        // ── Priority 3: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new LabeledSequence(bb, "3: Loot");
        lootSeq.AddChild(new IsLeaderOrNearLeader(bb));
        lootSeq.AddChild(new NoRevealedEnemies(bb, healSearchRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 4: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new LabeledSequence(bb, "4: Items");
        worldItemSeq.AddChild(new IsLeaderOrNearLeader(bb));
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, healSearchRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 5: YIELD ITEM SPACE ────────────────────────────────────
        var yieldSeq = new LabeledSequence(bb, "5: Yield Space");
        yieldSeq.AddChild(new YieldItemSpace(bb));
        root.AddChild(yieldSeq);

        // ── Priority 6: FOLLOW LEADER ────────────────────────────────────────
        // No enemy guard — the healer's job is to stay near the party at all times.
        var followSeq = new LabeledSequence(bb, "6: Follow");
        followSeq.AddChild(new FollowLeader(bb));
        root.AddChild(followSeq);

        // ── Priority 7: WAIT FOR PARTY UPGRADES (leader only) ───────────────
        var waitSeq = new LabeledSequence(bb, "7: Wait Upgrades");
        waitSeq.AddChild(new WaitForPartyUpgrades(bb));
        root.AddChild(waitSeq);

        // ── Priority 8: EXPLORE (fallback if this healer is somehow the leader) ─
        var exploreSeq = new LabeledSequence(bb, "8: Explore");
        exploreSeq.AddChild(new FindFogCluster(bb));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        root.AddChild(exploreSeq);

        return root;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Heal search radius
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healSearchRange);

        // Heal cast radius
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, healRange);

        // Danger zone
        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, healSearchRange * 1.5f);
    }
}
