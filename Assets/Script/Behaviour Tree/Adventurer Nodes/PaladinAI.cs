using UnityEngine;

/// <summary>
/// Paladin AI — Priority: Extract > Potions > Flee > Self-Heal(no healer) > Heal > Attack > Follow > Explore.
///
/// The paladin is a hybrid damage/support unit.
///
///   HEALING requires a Staff. Without a staff the heal sequences are skipped and
///   the paladin acts as a pure melee/ranged damage dealer.
///
/// FUZZY LOGIC
///
///   FuzzySelector for heal tiers (same as HealerAI):
///     Critical score = RampDown(lowestAllyHP, 0, criticalThreshold)
///     Normal score   = RampDown(lowestAllyHP, criticalThreshold, healThreshold)
///     The Paladin picks whichever heal is most urgent rather than snapping
///     between two hard thresholds.
///
///   FuzzyHPGuard on attack — party-aware thresholds:
///     Healer alive   → lo=0.25, hi=0.65, threshold=0.40  (retreats ~41 % HP)
///     No healer alive→ lo=0.50, hi=0.90, threshold=0.70  (retreats ~73 % HP)
///     When the HealerAI dies the Paladin fights only when near full health —
///     it must stay alive to sustain the party. Already-running combat continues
///     uninterrupted (Sequence is non-reactive once AdaptiveAttack is Running).
///
/// PARTY AWARENESS
///
///   IsHealerAlive — checked every tick:
///     • No healer alive → Priority 3 self-heal at 85 % HP threshold kicks in,
///       and the attack guard tightens drastically (threshold 0.70).
///     • Healer alive    → normal behavior.
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
        // Paladin personality: moderate — same hiHP as Archer (0.50).
        // The Paladin CAN self-heal (priority 4), so it's not as desperate
        // as a pure caster, but it still needs to retreat to cast safely.
        var fleeSeq = new LabeledSequence(bb, "3: Flee");
        fleeSeq.AddChild(new FleeFromNearestEnemy(bb,
            loHPFraction:   0.15f,
            hiHPFraction:   0.50f,
            threshold:      0.35f,
            detectionRange: enemyDetectionRange,
            fleeDistance:   6f));
        root.AddChild(fleeSeq);

        // ── Priority 4: SELF-HEAL when no dedicated healer is alive ──────────
        // When the party's HealerAI dies, the Paladin must keep itself healthy
        // to sustain the party long-term.  Heals self at 85 % HP threshold using
        // a tiny search radius (0.5 u) so only the Paladin itself is ever selected.
        var noHealerGate = new Inverter(bb);
        noHealerGate.AddChild(new IsHealerAlive(bb));

        var noHealerSelfHeal = new LabeledSequence(bb, "4: Self-Heal (no healer)");
        noHealerSelfHeal.AddChild(noHealerGate);
        noHealerSelfHeal.AddChild(new HasWeaponType(bb, WeaponType.Staff));
        noHealerSelfHeal.AddChild(new FindInjuredAlly(bb, 0.85f, 0.5f, "healTarget", includeSelf: true));
        noHealerSelfHeal.AddChild(new HealTarget(bb, healRange, "healTarget"));
        root.AddChild(noHealerSelfHeal);

        // ── Priority 5: HEAL (fuzzy-scored, staff required) ──────────────────
        // FuzzySelector scores Critical vs Normal heal every tick.
        // Critical score → peaks when someone is near death.
        // Normal score   → active in the band between critical and normal thresholds.
        // When nobody needs healing both scores are 0 → Failure → fall through.
        float lowestFraction = 1f;

        var healCritSeq = new LabeledSequence(bb, "5a: Heal Critical");
        healCritSeq.AddChild(new HasWeaponType(bb, WeaponType.Staff));
        healCritSeq.AddChild(new FindInjuredAlly(bb, criticalHPThreshold, healSearchRange, "healTarget", includeSelf: true));
        healCritSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));

        var healNormSeq = new LabeledSequence(bb, "5b: Heal Normal");
        healNormSeq.AddChild(new HasWeaponType(bb, WeaponType.Staff));
        healNormSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        healNormSeq.AddChild(new FindInjuredAlly(bb, healThreshold, healSearchRange, "healTarget", includeSelf: true));
        healNormSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));

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
            FuzzyLogic.RampDown(lowestFraction, critThresh, normThresh));

        root.AddChild(healFuzzy);

        // ── Priority 6: ATTACK (party-aware) ─────────────────────────────────
        // Healer alive   → normal FuzzyHPGuard (threshold 0.40): Paladin fights
        //   as long as it's reasonably healthy — a healer will top it up.
        // No healer alive→ tight FuzzyHPGuard (threshold 0.70): Paladin only
        //   engages when near full health. The two guards can't both pass at a low
        //   HP value (mathematically disjoint), so the Paladin always retreats when
        //   hurt and no healer is available.
        var attackBranch = new Selector(bb);

        var attackWithHealer = new LabeledSequence(bb, "6a: Attack (healer alive)");
        attackWithHealer.AddChild(new IsHealerAlive(bb));
        attackWithHealer.AddChild(new FuzzyHPGuard(bb, loHPFraction: 0.25f, hiHPFraction: 0.65f, threshold: 0.40f));
        attackWithHealer.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                         detectionRange: enemyDetectionRange,
                                                         wallLayers: wallLayers));
        attackWithHealer.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));

        var attackNoHealer = new LabeledSequence(bb, "6b: Attack (no healer, cautious)");
        attackNoHealer.AddChild(new FuzzyHPGuard(bb, loHPFraction: 0.50f, hiHPFraction: 0.90f, threshold: 0.70f));
        attackNoHealer.AddChild(new SelectCombatTarget(bb, selfDefenseRange: 3f,
                                                       detectionRange: enemyDetectionRange,
                                                       wallLayers: wallLayers));
        attackNoHealer.AddChild(new AdaptiveAttack(bb, enemyLayer, kiteDistance, wallLayers));

        attackBranch.AddChild(attackWithHealer);
        attackBranch.AddChild(attackNoHealer);
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

        // ── Priority 10: PICK UP POTIONS ─────────────────────────────────────
        var potionSeq = new LabeledSequence(bb, "10: Pickup Potion");
        potionSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange, wallLayers));
        potionSeq.AddChild(new FindPotionInRange(bb, 12f));
        potionSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        root.AddChild(potionSeq);

        // ── Priority 11: SHARE SURPLUS POTIONS (fuzzy) ───────────────────────
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the lowest HP fraction among all Player-tagged GameObjects within
    /// <paramref name="searchRange"/> of this paladin.  Returns 1.0 when no
    /// injured allies are found (so both fuzzy heal scores evaluate to 0).
    /// </summary>
    private float SampleLowestHPFraction(float searchRange)
    {
        float lowest = 1f;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (var p in players)
        {
            if (p == null) continue;
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

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange);

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, healSearchRange);
    }
}
