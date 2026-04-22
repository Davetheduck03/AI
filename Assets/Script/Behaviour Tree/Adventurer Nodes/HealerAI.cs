using UnityEngine;

/// <summary>
/// Healer AI — Priority: Extract > Potions > Flee > Heal (fuzzy) > Loot > Items > Follow > Explore.
///
/// HEALING — FUZZY SELECTOR
///   The two heal tiers (critical and normal) are now wrapped in a FuzzySelector.
///   Each tick it scores both sequences and runs whichever is most urgent:
///
///     Critical score  = RampDown(lowestAllyHP, 0, criticalThreshold)
///                     → 1 when someone is nearly dead, 0 above the critical band.
///
///     Normal score    = RampDown(lowestAllyHP, criticalThreshold, healThreshold)
///                     → 1 at critical threshold, 0 at normal threshold.
///
///   Critical always outscores Normal when an ally is below criticalHPThreshold,
///   but the transition is smooth rather than a hard snap.
///   When nobody is below healThreshold both scores are 0 → FuzzySelector returns
///   Failure → the tree falls through to Loot / Follow / Explore.
///
/// MANA
///   HealTarget checks healManaCost each cast (set in the Inspector on the
///   prefab's ManaComponent).  When the healer runs dry it returns Running,
///   waiting for regen or a mana potion to kick in.
///
/// POTIONS
///   UseHealthPotion fires at 50 % HP (before the healer needs to be healed by itself).
///   UseManaPotion fires at 35 % mana so the healer can keep casting.
/// </summary>
public class HealerAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask wallLayers;

    [Header("Healing")]
    [Tooltip("Allies at or below this HP fraction are treated as critical — healer pushes through combat to reach them.")]
    [SerializeField] private float criticalHPThreshold = 0.35f;

    [Tooltip("Allies at or below this HP fraction receive a normal heal.")]
    [SerializeField] private float healThreshold = 0.75f;

    [Tooltip("World-unit radius within which the healer scans for injured allies.")]
    [SerializeField] private float healSearchRange = 14f;

    [Tooltip("World-unit radius the healer must be within before casting.")]
    [SerializeField] private float healRange = 4.0f;

    [Header("Self-preservation")]
    [Tooltip("Enemy detection radius used for the enemy guards on loot / explore.")]
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

        // ── Priority 1: USE HEALTH POTION ────────────────────────────────────
        var hpPotSeq = new LabeledSequence(bb, "1: Health Potion");
        hpPotSeq.AddChild(new UseHealthPotion(bb, threshold: 0.5f));
        root.AddChild(hpPotSeq);

        // ── Priority 2: USE MANA POTION ──────────────────────────────────────
        var manaPotSeq = new LabeledSequence(bb, "2: Mana Potion");
        manaPotSeq.AddChild(new UseManaPotion(bb, threshold: 0.35f));
        root.AddChild(manaPotSeq);

        // ── Priority 3: FLEE (fuzzy fear) ────────────────────────────────────
        // Healer personality: most fearful — hiHP 0.60 means the healer begins
        // panicking at 60 % HP and flees the farthest (9 u).  A live healer is
        // critical to party survival, so they should never die in melee.
        // dangerRange is reused for detection — healers use a tighter scan
        // radius than combat classes.
        var fleeSeq = new LabeledSequence(bb, "3: Flee");
        fleeSeq.AddChild(new FleeFromNearestEnemy(bb,
            loHPFraction:   0.15f,
            hiHPFraction:   0.60f,
            threshold:      0.30f,
            detectionRange: dangerRange,
            fleeDistance:   9f));
        root.AddChild(fleeSeq);

        // ── Priority 4: HEAL (fuzzy-scored) ──────────────────────────────────
        // FuzzySelector scores Critical vs Normal heal every tick and runs whichever
        // is more urgent.  Both scores drop to 0 when no ally needs healing, causing
        // the selector to return Failure and the tree to fall through.
        //
        // We need a stable reference to the lowest ally HP fraction for scoring.
        // A small closure reads it fresh each tick from the tag scan.
        float lowestFraction = 1f;

        var healCritSeq = new LabeledSequence(bb, "4a: Heal Critical");
        healCritSeq.AddChild(new FindInjuredAlly(bb, criticalHPThreshold, healSearchRange, "healTarget"));
        healCritSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));

        var healNormSeq = new LabeledSequence(bb, "4b: Heal Normal");
        healNormSeq.AddChild(new FindInjuredAlly(bb, healThreshold, healSearchRange, "healTarget"));
        healNormSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));

        // Score functions read lowestFraction from the party each evaluation.
        // We capture criticalHPThreshold and healThreshold by value via the closure.
        float critThresh = criticalHPThreshold;
        float normThresh = healThreshold;
        float range      = healSearchRange;

        var healFuzzy = new FuzzySelector(bb);
        healFuzzy.Add(healCritSeq, () =>
        {
            lowestFraction = SampleLowestHPFraction(range);
            return FuzzyLogic.RampDown(lowestFraction, 0f, critThresh);
        });
        healFuzzy.Add(healNormSeq, () =>
        {
            // lowestFraction already updated by the critical score func above
            return FuzzyLogic.RampDown(lowestFraction, critThresh, normThresh);
        });

        root.AddChild(healFuzzy);

        // ── Priority 5: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new LabeledSequence(bb, "5: Loot");
        lootSeq.AddChild(new IsLeaderOrNearLeader(bb));
        lootSeq.AddChild(new NoRevealedEnemies(bb, healSearchRange, wallLayers));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 6: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new LabeledSequence(bb, "6: Items");
        worldItemSeq.AddChild(new IsLeaderOrNearLeader(bb));
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, healSearchRange, wallLayers));
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
        potionSeq.AddChild(new NoRevealedEnemies(bb, dangerRange, wallLayers));
        potionSeq.AddChild(new FindPotionInRange(bb, 12f));
        potionSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        root.AddChild(potionSeq);

        // ── Priority 9: SHARE SURPLUS POTIONS (fuzzy) ────────────────────────
        // Healer uses dangerRange as the enemy guard distance (more conservative).
        var sharePotSeq = new LabeledSequence(bb, "9: Share Potion");
        sharePotSeq.AddChild(new NoRevealedEnemies(bb, dangerRange, wallLayers));
        sharePotSeq.AddChild(new SharePotion(bb, searchRange: 10f));
        root.AddChild(sharePotSeq);

        // ── Priority 10: FOLLOW LEADER ────────────────────────────────────────
        var followSeq = new LabeledSequence(bb, "10: Follow");
        followSeq.AddChild(new FollowLeader(bb));
        root.AddChild(followSeq);

        // ── Priority 11: WAIT FOR PARTY UPGRADES (leader only) ───────────────
        var waitSeq = new LabeledSequence(bb, "11: Wait Upgrades");
        waitSeq.AddChild(new WaitForPartyUpgrades(bb));
        root.AddChild(waitSeq);

        // ── Priority 12: EXPLORE (fallback) ──────────────────────────────────
        var exploreSeq = new LabeledSequence(bb, "12: Explore");
        exploreSeq.AddChild(new FindFogCluster(bb));
        exploreSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        root.AddChild(exploreSeq);

        return root;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the lowest HP fraction among all Player-tagged GameObjects within
    /// <paramref name="searchRange"/> of this healer.  Returns 1.0 when no
    /// injured allies are found (so both fuzzy scores evaluate to 0).
    /// </summary>
    private float SampleLowestHPFraction(float searchRange)
    {
        float lowest = 1f;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (var p in players)
        {
            if (p == null || p.transform == transform) continue;
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist > searchRange) continue;

            var hc = p.GetComponent<HealthComponent>();
            if (hc == null) continue;

            float frac = hc.currentHealth / hc.maxHealth;
            if (frac < lowest) lowest = frac;
        }

        return lowest;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, healSearchRange);

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, healRange);

        Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, healSearchRange * 1.5f);
    }
}
