// KnightAI.cs
using UnityEngine;

/// <summary>
/// Knight AI — Priority: Extract > Potions > Flee > Leader Rally > Wary Explore > Attack > Loot > Pick up items > Follow > Explore.
///
/// Combat adapts to the equipped weapon at runtime:
///   Melee weapon (Sword, LongSword, Dagger) → charges in and attacks.
///   Ranged weapon (Bow)                     → kites at preferred range.
///
/// FUZZY LOGIC
///   FuzzyHPGuard on attack — Knight fights until ~35 % HP willingness, then
///   falls back to Follow so the healer can restore them before re-engaging.
///   Personality: aggressive (lo=0.20, hi=0.60) — retreats latest of all melee classes.
///
/// PARTY AWARENESS
///   IsOnlyMeleeUnit — checked every tick at the start of the attack branch:
///     • Other melee alive → normal mode  (retreats ~33 % HP).
///     • Sole frontliner   → cautious mode (retreats ~50 % HP, lo=0.30, hi=0.70).
///       The last melee unit cannot afford to go down and leave the squishies exposed.
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

        // ── Priority 1: USE HEALTH POTION ────────────────────────────────────
        var hpPotSeq = new LabeledSequence(bb, "1: Health Potion");
        hpPotSeq.AddChild(new UseHealthPotion(bb, threshold: 0.5f));
        root.AddChild(hpPotSeq);

        // ── Priority 2: USE MANA POTION ──────────────────────────────────────
        var manaPotSeq = new LabeledSequence(bb, "2: Mana Potion");
        manaPotSeq.AddChild(new UseManaPotion(bb, threshold: 0.35f));
        root.AddChild(manaPotSeq);

        // ── Priority 3: FLEE (fuzzy fear) ────────────────────────────────────
        // Kicks in when the Knight's HP drops below ~43 % (fear ≥ 0.35).
        // The root Selector is reactive, so this interrupts any ongoing combat
        // immediately — the Knight sprints away from the nearest enemy until
        // healed above 50 % HP, then falls back through to the attack branch.
        // Knight personality: slightly less cowardly than ranged classes
        // (hiHP 0.45 vs 0.50), reflecting their melee resilience.
        //
        // FOLLOWER OVERRIDE: When the leader is actively fighting, followers
        // suppress their personal flee and rush to help instead.  The Inverter
        // gate fails (returning Failure) when the leader has a live combat target
        // AND this hero is a follower — the whole flee sequence short-circuits.
        var fleeSeq = new LabeledSequence(bb, "3: Flee");
        var notLeaderInCombat = new Inverter(bb);
        notLeaderInCombat.AddChild(new IsLeaderInCombat(bb));
        fleeSeq.AddChild(notLeaderInCombat);
        fleeSeq.AddChild(new FleeFromNearestEnemy(bb,
            loHPFraction:   0.15f,
            hiHPFraction:   0.45f,
            threshold:      0.35f,
            detectionRange: enemyDetectionRange,
            fleeDistance:   5f));
        root.AddChild(fleeSeq);

        // ── Priority 4: LEADER RALLY ──────────────────────────────────────────
        // Leader-only: when any ally is below 60 % HP and enemies are in range,
        // the leader finds the safest nearby spot and holds there so followers
        // can converge, receive SharePotion transfers, and recover before
        // re-engaging.  Followers are NOT included here — they come via
        // FollowLeader once the leader is stationary at the rally point.
        var rallySeq = new LabeledSequence(bb, "4: Leader Rally");
        rallySeq.AddChild(new LeaderRally(bb,
            partyHurtThreshold: 0.60f,
            enemyScanRange:     enemyDetectionRange,
            rallyDistance:      7f,
            holdRange:          1.5f));
        root.AddChild(rallySeq);

        // ── Priority 5: WARY EXPLORE (fuzzy: hurt + threatened → seek safety) ──
        // When the Knight is moderately hurt AND an enemy is nearby, the fuzzy
        // waryScore (fearScore × threatScore) passes the threshold and the Knight
        // diverts to unexplored fog rather than charging in.  Both factors must be
        // elevated simultaneously — a healthy Knight ignores nearby enemies and
        // still attacks; a hurt Knight with no enemies nearby just heals up.
        // If no fog cluster exists (fully explored map) FindFogCluster fails and
        // the branch falls through to Attack — last-resort combat.
        // Knight: less cowardly than ranged classes (hiHP 0.65 = calm at 65% HP).
        var warySeq = new LabeledSequence(bb, "5: Wary Explore");
        warySeq.AddChild(new WaryExploreGuard(bb,
            loHPFraction:   0.20f,
            hiHPFraction:   0.65f,
            nearDist:       2.0f,
            detectionRange: enemyDetectionRange,
            threshold:      0.20f));
        warySeq.AddChild(new FindFogCluster(bb));
        warySeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        root.AddChild(warySeq);

        // ── Priority 6: ATTACK (party-aware) ─────────────────────────────────
        // Normal mode   — Knight fights until ~33 % HP (aggressive personality).
        // Solo melee    — Knight is the only surviving frontliner; retreats at ~50 % HP
        //                 because going down leaves the squishies completely exposed.
        //
        // Inner Selector: normal branch is gated by NOT IsOnlyMeleeUnit, so when solo
        // it fails immediately and the cautious branch runs instead.  When solo AND HP
        // is too low for cautious mode, the whole attackBranch fails → Knight retreats.
        var notSolo = new Inverter(bb);
        notSolo.AddChild(new IsOnlyMeleeUnit(bb));

        var attackNormal = new LabeledSequence(bb, "6a: Attack (normal)");
        attackNormal.AddChild(notSolo);
        attackNormal.AddChild(new FuzzyHPGuard(bb, loHPFraction: 0.20f, hiHPFraction: 0.60f, threshold: 0.35f));
        attackNormal.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                     detectionRange: enemyDetectionRange,
                                                     wallLayers: wallLayers));
        attackNormal.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));

        var attackSolo = new LabeledSequence(bb, "6b: Attack (solo melee, cautious)");
        attackSolo.AddChild(new IsOnlyMeleeUnit(bb));
        attackSolo.AddChild(new FuzzyHPGuard(bb, loHPFraction: 0.30f, hiHPFraction: 0.70f, threshold: 0.50f));
        attackSolo.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                   detectionRange: enemyDetectionRange,
                                                   wallLayers: wallLayers));
        attackSolo.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));

        var attackBranch = new Selector(bb);
        attackBranch.AddChild(attackNormal);
        attackBranch.AddChild(attackSolo);
        root.AddChild(attackBranch);

        // ── Priority 7: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new LabeledSequence(bb, "7: Loot");
        lootSeq.AddChild(new IsLeaderOrNearLeader(bb));
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 8: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new LabeledSequence(bb, "8: Items");
        worldItemSeq.AddChild(new IsLeaderOrNearLeader(bb));
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 16f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 9: YIELD ITEM SPACE ────────────────────────────────────
        var yieldSeq = new LabeledSequence(bb, "9: Yield Space");
        yieldSeq.AddChild(new YieldItemSpace(bb));
        root.AddChild(yieldSeq);

        // ── Priority 10: PICK UP POTIONS ──────────────────────────────────────
        // Any hero (leader or follower) will detour to a nearby potion they have
        // room for, as long as no enemies are visible. Contact pickup fires
        // automatically when the hero walks over the WorldItem.
        var potionSeq = new LabeledSequence(bb, "10: Pickup Potion");
        potionSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        potionSeq.AddChild(new FindPotionInRange(bb, 12f));
        potionSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        root.AddChild(potionSeq);

        // ── Priority 11: SHARE SURPLUS POTIONS (fuzzy, safe moments) ─────────
        // Opportunistic sharing when no enemies are visible.  The leader rally
        // system ensures injured allies converge to a safe point where SharePotion
        // can fire without the enemy gate blocking it.
        var sharePotSeq = new LabeledSequence(bb, "11: Share Potion");
        sharePotSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        sharePotSeq.AddChild(new SharePotion(bb, searchRange: 10f));
        root.AddChild(sharePotSeq);

        // ── Priority 12: FOLLOW LEADER (followers only) ──────────────────────
        var followSeq = new LabeledSequence(bb, "12: Follow");
        followSeq.AddChild(new FollowLeader(bb));
        root.AddChild(followSeq);

        // ── Priority 13: WAIT FOR PARTY UPGRADES (leader only) ───────────────
        var waitSeq = new LabeledSequence(bb, "13: Wait Upgrades");
        waitSeq.AddChild(new WaitForPartyUpgrades(bb));
        root.AddChild(waitSeq);

        // ── Priority 14: EXPLORE (leader + fallback for all) ─────────────────
        var exploreSeq = new LabeledSequence(bb, "14: Explore");
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
