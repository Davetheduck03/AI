using UnityEngine;

/// <summary>
/// Archer AI — Priority: Extract > Attack (at range) > Guard relic carrier > Loot > Pick up items > Explore.
///
/// Key differences from KnightAI:
///   - Larger detection range; stays at bow range instead of charging in.
///   - Approach distance is driven by the equipped weapon's AttackRange, not a hardcoded value.
///   - "Guard relic carrier" sequence: if a teammate holds the relic the archer
///     follows them rather than wandering off to loot.
/// </summary>
public class ArcherAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;

    [Header("Detection")]
    [SerializeField] private float enemyDetectionRange = 14f;
    // Approach/standoff distance is driven by the equipped weapon's range
    // (WeaponSO.range → DamageComponent.AttackRange) — no inspector field needed.

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
        // MoveAndAttack reads AttackRange live, so the archer automatically keeps
        // the distance matching whatever bow is equipped.
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestRevealedEnemy(bb, enemyDetectionRange));
        attackSeq.AddChild(new MoveAndAttack(bb, enemyLayer));
        root.AddChild(attackSeq);

        // ── Priority 2: GUARD RELIC CARRIER ─────────────────────────────────
        // Follows the relic-carrying teammate at close range to cover their escape.
        var guardSeq = new Sequence(bb);
        guardSeq.AddChild(new IsRelicHeldByTeammate(bb, team));
        guardSeq.AddChild(new MoveTowardsTarget(bb, 2f, "relicHolder"));
        root.AddChild(guardSeq);

        // ── Priority 3: LOOT CHESTS ──────────────────────────────────────────
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ── Priority 4: PICK UP WORLD ITEMS ─────────────────────────────────
        var worldItemSeq = new Sequence(bb);
        worldItemSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
        worldItemSeq.AddChild(new EvaluateNearbyItems(bb, searchRange: 8f));
        worldItemSeq.AddChild(new MoveTowardsTarget(bb, 0.5f, "itemTarget"));
        worldItemSeq.AddChild(new PickupItem(bb));
        root.AddChild(worldItemSeq);

        // ── Priority 5: EXPLORE ──────────────────────────────────────────────
        var exploreSeq = new Sequence(bb);
        exploreSeq.AddChild(new NoRevealedEnemies(bb, enemyDetectionRange));
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
