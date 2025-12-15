using UnityEngine;

/// <summary>
/// Knight AI with proper combat priority.
/// Interrupts exploration when enemies are spotted.
/// Priority: Attack > Loot (when safe) > Explore (when safe)
/// </summary>
public class KnightAI : BehaviorTreeRunner
{
    [SerializeField] private LayerMask enemyLayer;

    [Header("Combat Settings")]
    [SerializeField] private float combatMemoryDuration = 2f;  // Stay alert for 2s after seeing enemy

    private float lastEnemySeenTime = -100f;  // Track when we last saw an enemy

    protected override Node BuildTree()
    {
        var root = new Selector(bb);

        // ============================================
        // Priority 1: ATTACK (Always checked first!)
        // ============================================
        var attackSeq = new Sequence(bb);
        attackSeq.AddChild(new FindNearestEnemy(bb, 10f));      
        attackSeq.AddChild(new IsTargetRevealed(bb));           
        attackSeq.AddChild(new MoveTowardsTarget(bb, 1f));     
        attackSeq.AddChild(new IsInAttackRange(bb, 2f));       
        attackSeq.AddChild(new AttackTarget(bb, 2f, 1f, enemyLayer));
        root.AddChild(attackSeq);

        // ============================================
        // Priority 2: LOOT (Only when no enemies)
        // ============================================
        var lootSeq = new Sequence(bb);
        lootSeq.AddChild(new CheckCombatCooldown(bb, this));
        lootSeq.AddChild(new FindLootInRange(bb, 10f));
        lootSeq.AddChild(new IsTargetRevealed(bb));
        lootSeq.AddChild(new MoveTowardsTarget(bb, 0.5f));
        lootSeq.AddChild(new LootTarget(bb));
        root.AddChild(lootSeq);

        // ============================================
        // Priority 3: EXPLORE CLUSTERS (Only when safe)
        // ============================================
        var clusterSeq = new Sequence(bb);
        clusterSeq.AddChild(new CheckCombatCooldown(bb, this));
        clusterSeq.AddChild(new FindFogCluster(bb, 50f));
        clusterSeq.AddChild(new MoveTowardsTarget(bb, 3f));
        root.AddChild(clusterSeq);

        // ============================================
        // Priority 4: BASIC EXPLORATION (Last resort)
        // ============================================
        var basicExploreSeq = new Sequence(bb);
        basicExploreSeq.AddChild(new CheckCombatCooldown(bb, this));
        basicExploreSeq.AddChild(new FindUnexploredArea(bb, 50f));
        basicExploreSeq.AddChild(new MoveTowardsTarget(bb, 3f));
        root.AddChild(basicExploreSeq);

        return root;
    }

    /// <summary>
    /// Called by FindNearestEnemy when an enemy is spotted.
    /// Sets the knight into "combat mode" for a few seconds.
    /// </summary>
    public void NotifyEnemyFound()
    {
        lastEnemySeenTime = Time.time;
        Debug.Log("[KnightAI] Enemy spotted! Entering combat mode.");
    }

    /// <summary>
    /// Returns true if recently saw an enemy (within combatMemoryDuration).
    /// </summary>
    public bool IsInCombat()
    {
        return Time.time - lastEnemySeenTime < combatMemoryDuration;
    }

    // Optional: Visual debug in Scene view
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Draw combat status
        Gizmos.color = IsInCombat() ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}