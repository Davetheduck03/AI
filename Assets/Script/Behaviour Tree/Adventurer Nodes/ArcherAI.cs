using UnityEngine;

/// <summary>
/// Archer AI — Priority: Extract > Potions > Flee > Attack > Guard relic > Loot > Items > Follow > Explore.
///
/// Uses AdaptiveAttack so behaviour automatically matches the equipped weapon:
///   Bow equipped   → kites at preferred range (normal archer behaviour).
///   Melee equipped → charges in like a knight (unusual but handled gracefully).
///
/// FUZZY LOGIC
///   FuzzyHPGuard on attack — Archers are fragile; they disengage earlier than melee.
///   Personality: cautious (lo=0.30, hi=0.70, threshold=0.45) — retreats at ~48 % HP.
/// </summary>
public class ArcherAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask wallLayers;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 14f;

    [Header("Kiting (when bow equipped)")]
    [Tooltip("Preferred standoff distance. Clamped to attackRange - 0.3 automatically.")]
    [SerializeField] private float kiteDistance = 5.0f;

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

        // ── Priority 1: USE HEALTH POTION ────────────────────────────────────
        var hpPotSeq = new LabeledSequence(bb, "1: Health Potion");
        hpPotSeq.AddChild(new UseHealthPotion(bb, threshold: 0.5f));
        root.AddChild(hpPotSeq);

        // ── Priority 2: USE MANA POTION ──────────────────────────────────────
        var manaPotSeq = new LabeledSequence(bb, "2: Mana Potion");
        manaPotSeq.AddChild(new UseManaPotion(bb, threshold: 0.35f));
        root.AddChild(manaPotSeq);

        // ── Priority 3: FLEE (fuzzy fear) ────────────────────────────────────
        // Archer personality: more skittish than melee — hiHP 0.50 means the
        // fear curve reaches 0 at 50 % HP (same as default), matching the
        // Archer's generally cautious combat personality.
        var fleeSeq = new LabeledSequence(bb, "3: Flee");
        fleeSeq.AddChild(new FleeFromNearestEnemy(bb,
            loHPFraction:   0.15f,
            hiHPFraction:   0.50f,
            threshold:      0.35f,
            detectionRange: enemyDetectionRange,
            fleeDistance:   7f));   // Archers flee further — they need standoff room
        root.AddChild(fleeSeq);

        // ── Priority 4: ATTACK (adaptive) ────────────────────────────────────
        // FuzzyHPGuard: Archers are fragile — they back off earlier than melee classes.
        // hi=0.70 means the Archer is never fully willing below 70 % HP, and already
        // retreating at ~48 % HP (threshold 0.45 on the ramp).
        var attackSeq = new LabeledSequence(bb, "4: Attack");
        attackSeq.AddChild(new FuzzyHPGuard(bb, loHPFraction: 0.30f, hiHPFraction: 0.70f, threshold: 0.45f));
        attackSeq.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                  detectionRange: enemyDetectionRange,
                                                  wallLayers: wallLayers));
        attackSeq.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));
        root.AddChild(attackSeq);

        // ── Priority 5: GUARD RELIC CARRIER ─────────────────────────────────
        var guardSeq = new LabeledSequence(bb, "5: Guard Relic");
        guardSeq.AddChild(new IsRelicHeldByTeammate(bb, team));
        guardSeq.AddChild(new MoveTowardsTarget(bb, 2f, "relicHolder"));
        root.AddChild(guardSeq);

        // ── Priority 6: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new LabeledSequence(bb, "6: Loot");
        lootSeq.AddChild(new IsLeaderOrNearLeader(bb));
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 7: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new LabeledSequence(bb, "7: Items");
        worldItemSeq.AddChild(new IsLeaderOrNearLeader(bb));
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 8: YIELD ITEM SPACE ────────────────────────────────────
        var yieldSeq = new LabeledSequence(bb, "8: Yield Space");
        yieldSeq.AddChild(new YieldItemSpace(bb));
        root.AddChild(yieldSeq);

        // ── Priority 9: PICK UP POTIONS ──────────────────────────────────────
        var potionSeq = new LabeledSequence(bb, "9: Pickup Potion");
        potionSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        potionSeq.AddChild(new FindPotionInRange(bb, 12f));
        potionSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        root.AddChild(potionSeq);

        // ── Priority 10: SHARE SURPLUS POTIONS (fuzzy) ───────────────────────
        var sharePotSeq = new LabeledSequence(bb, "10: Share Potion");
        sharePotSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        sharePotSeq.AddChild(new SharePotion(bb, searchRange: 10f));
        root.AddChild(sharePotSeq);

        // ── Priority 11: FOLLOW LEADER (followers only) ──────────────────────
        var followSeq = new LabeledSequence(bb, "11: Follow");
        followSeq.AddChild(new FollowLeader(bb));
        root.AddChild(followSeq);

        // ── Priority 12: WAIT FOR PARTY UPGRADES (leader only) ───────────────
        var waitSeq = new LabeledSequence(bb, "12: Wait Upgrades");
        waitSeq.AddChild(new WaitForPartyUpgrades(bb));
        root.AddChild(waitSeq);

        // ── Priority 13: EXPLORE (leader + fallback for all) ─────────────────
        var exploreSeq = new LabeledSequence(bb, "13: Explore");
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

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, kiteDistance);
    }
}
