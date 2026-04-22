using UnityEngine;

/// <summary>
/// Mage AI — Priority: Extract > Potions > Flee > Attack > Loot > Items > Follow > Explore.
///
/// The mage attacks only when a Staff is equipped. Without a staff the attack sequence
/// is skipped entirely — the mage will loot, follow, and explore but not engage.
///
/// Staves are treated as ranged weapons by AdaptiveAttack, so the mage always kites
/// at <see cref="kiteDistance"/> rather than charging in.
///
/// FUZZY LOGIC
///   FuzzyManaGuard — Mage becomes reluctant to engage as mana drains, and refuses
///   entirely below ~15 % mana. Falls through to Follow/Explore while regenerating.
///
///   FuzzyHPGuard — Mage is fragile; disengages proactively at ~40 % HP
///   (lo=0.25, hi=0.60, threshold=0.40). Stacks with FuzzyManaGuard: BOTH must
///   pass for the Mage to initiate combat.
///
/// Detection range is wider than other classes — mages spot threats early.
/// </summary>
public class MageAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask wallLayers;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 16f;

    [Header("Kiting")]
    [Tooltip("Preferred standoff distance. Clamped to attackRange - 0.3 automatically.")]
    [SerializeField] private float kiteDistance = 3.8f;

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
        // Mage personality: extremely cowardly — hiHP 0.55 means the Mage starts
        // feeling scared earlier than any other class and flees aggressively
        // (fleeDistance 8 u).  Stacks naturally with FuzzyHPGuard and FuzzyManaGuard
        // on the attack branch — a panicked Mage won't fight at all.
        var fleeSeq = new LabeledSequence(bb, "3: Flee");
        fleeSeq.AddChild(new FleeFromNearestEnemy(bb,
            loHPFraction:   0.15f,
            hiHPFraction:   0.55f,
            threshold:      0.35f,
            detectionRange: enemyDetectionRange,
            fleeDistance:   8f));   // Mages want a lot of space
        root.AddChild(fleeSeq);

        // ── Priority 4: ATTACK (staff + mana + HP required) ─────────────────
        // FuzzyManaGuard: refuses to engage when mana is depleted — Mage waits
        //   for regen rather than standing uselessly in range.
        // FuzzyHPGuard: Mage is a fragile caster — disengages at ~40 % HP.
        //   Both guards must pass. If either fails the Mage falls through to
        //   Follow/Explore until resources recover.
        var attackSeq = new LabeledSequence(bb, "4: Attack");
        attackSeq.AddChild(new HasWeaponType(bb, WeaponType.Staff));
        attackSeq.AddChild(new FuzzyManaGuard(bb, loManaFraction: 0.15f, hiManaFraction: 0.55f, threshold: 0.40f));
        attackSeq.AddChild(new FuzzyHPGuard(bb,   loHPFraction:   0.25f, hiHPFraction:   0.60f, threshold: 0.40f));
        attackSeq.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                  detectionRange: enemyDetectionRange,
                                                  wallLayers: wallLayers));
        attackSeq.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));
        root.AddChild(attackSeq);

        // ── Priority 5: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new LabeledSequence(bb, "5: Loot");
        lootSeq.AddChild(new IsLeaderOrNearLeader(bb));
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 6: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new LabeledSequence(bb, "6: Items");
        worldItemSeq.AddChild(new IsLeaderOrNearLeader(bb));
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 7: YIELD ITEM SPACE ────────────────────────────────────
        var yieldSeq = new LabeledSequence(bb, "7: Yield Space");
        yieldSeq.AddChild(new YieldItemSpace(bb));
        root.AddChild(yieldSeq);

        // ── Priority 8: PICK UP POTIONS ──────────────────────────────────────
        var potionSeq = new LabeledSequence(bb, "8: Pickup Potion");
        potionSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        potionSeq.AddChild(new FindPotionInRange(bb, 12f));
        potionSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        root.AddChild(potionSeq);

        // ── Priority 9: SHARE SURPLUS POTIONS (fuzzy) ────────────────────────
        var sharePotSeq = new LabeledSequence(bb, "9: Share Potion");
        sharePotSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        sharePotSeq.AddChild(new SharePotion(bb, searchRange: 10f));
        root.AddChild(sharePotSeq);

        // ── Priority 10: FOLLOW LEADER (followers only) ───────────────────────
        var followSeq = new LabeledSequence(bb, "10: Follow");
        followSeq.AddChild(new FollowLeader(bb));
        root.AddChild(followSeq);

        // ── Priority 11: WAIT FOR PARTY UPGRADES (leader only) ───────────────
        var waitSeq = new LabeledSequence(bb, "11: Wait Upgrades");
        waitSeq.AddChild(new WaitForPartyUpgrades(bb));
        root.AddChild(waitSeq);

        // ── Priority 12: EXPLORE (leader + fallback for all) ─────────────────
        var exploreSeq = new LabeledSequence(bb, "12: Explore");
        exploreSeq.AddChild(new FindFogCluster(bb));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        root.AddChild(exploreSeq);

        return root;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange);

        Gizmos.color = new Color(0.8f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, kiteDistance);
    }
}
