using UnityEngine;

/// <summary>
/// Healer AI — Priority: Extract > Heal critical ally > Heal injured ally > Follow leader > Explore.
///
/// The healer never attacks enemies. Its sole contribution in combat is keeping the party
/// alive. Two healing tiers give finer control:
///
///   CRITICAL heal (no enemy guard) — fires even mid-combat when an ally is near death.
///   This lets the healer push through a fight to save a dying teammate.
///
///   NORMAL heal (enemy guard, soft range) — fires only when no enemies are immediately
///   nearby, so the healer doesn't wander into danger for a small top-up.
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
    [SerializeField] private float healRange = 2.5f;

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
        var extractSeq = new Sequence(bb);
        extractSeq.AddChild(new SetExtractionTarget(bb));
        extractSeq.AddChild(new MoveTowardsTarget(bb, 1.0f));
        extractSeq.AddChild(new TriggerWin(bb));
        root.AddChild(extractSeq);

        // ── Priority 1: HEAL CRITICAL ALLY ───────────────────────────────────
        // No NoRevealedEnemies guard — the healer will brave a fight to save a dying ally.
        var healCritSeq = new Sequence(bb);
        healCritSeq.AddChild(new FindInjuredAlly(bb, criticalHPThreshold, healSearchRange, "healTarget"));
        healCritSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));
        root.AddChild(healCritSeq);

        // ── Priority 2: HEAL INJURED ALLY ────────────────────────────────────
        // Only heal non-critically when enemies aren't immediately threatening.
        var healSeq = new Sequence(bb);
        healSeq.AddChild(new NoRevealedEnemies(bb, dangerRange, wallLayers));
        healSeq.AddChild(new FindInjuredAlly(bb, healThreshold, healSearchRange, "healTarget"));
        healSeq.AddChild(new HealTarget(bb, healRange, "healTarget"));
        root.AddChild(healSeq);

        // ── Priority 3: FOLLOW LEADER ────────────────────────────────────────
        // Shadow the leader so the healer remains close enough to respond quickly.
        var followSeq = new Sequence(bb);
        followSeq.AddChild(new NoRevealedEnemies(bb, dangerRange, wallLayers));
        followSeq.AddChild(new FollowLeader(bb, 1.5f));
        root.AddChild(followSeq);

        // ── Priority 4: EXPLORE (fallback if this healer is somehow the leader) ─
        var exploreSeq = new Sequence(bb);
        exploreSeq.AddChild(new NoRevealedEnemies(bb, dangerRange, wallLayers));
        exploreSeq.AddChild(new FindFogCluster(bb, 50f));
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

        // Danger zone (drives enemy guards)
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, dangerRange);
    }
}
