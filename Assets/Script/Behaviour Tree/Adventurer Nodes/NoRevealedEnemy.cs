using UnityEngine;

/// <summary>
/// CONDITION: Returns Success if NO actionable enemy is in range.
///
/// "Actionable" matches the same criteria used by SelectCombatTarget:
///   • Not in fog of war
///   • Within <paramref name="range"/>
///   • Has line-of-sight (when <paramref name="wallLayers"/> is non-zero)
///
/// WHY A GRACE PERIOD IS NECESSARY
/// ─────────────────────────────────
/// Without a grace period, an enemy at the edge of detection range flickers
/// in and out every ScanInterval (0.2 s): the node alternates
/// Failure → Success → Failure → Success at 5 Hz.  Each Success tick allows
/// the loot / world-item sequence to fire; each Failure tick kills it (Sequence
/// resets to index 0).  In between, the Explore sequence fires and writes a fog
/// cluster to bb["target"], then the loot sequence fires again and overwrites it
/// with the chest.  MoveTowardsTarget sees targetChanged every 0.2 s and
/// re-triggers A* each time — the hero twitches in place rather than moving.
///
/// Fix: once an enemy is detected, stay in "enemies present" mode for an extra
/// <see cref="GracePeriod"/> seconds before allowing the area to be declared
/// clear again.  This prevents a single scan miss at a range boundary from
/// immediately reopening loot/item sequences and thrashing the path planner.
/// </summary>
public class NoRevealedEnemies : Node
{
    private readonly float      _range;
    private readonly LayerMask  _wallLayers;
    private FogOfWarManager     _fogManager;

    // Throttle: FindGameObjectsWithTag is expensive and this node runs twice
    // per hero per BT tick (loot guard + world-item guard).
    private const float ScanInterval = 0.2f;
    private float       _nextScanTime = float.MinValue;
    private bool        _cachedClear  = true;

    // After detecting an enemy, hold the "enemies present" state for this
    // many seconds beyond the last detection before reporting the area clear.
    // Chosen to match the explore cluster refresh interval (0.8 s) so loot
    // and explore don't alternate faster than the path planner can react.
    private const float GracePeriod = 0.8f;
    private float       _clearAfter  = 0f;

    public NoRevealedEnemies(Blackboard bb, float range = 10f, LayerMask wallLayers = default) : base(bb)
    {
        _range      = range;
        _wallLayers = wallLayers;
        _fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        // If this hero is locked into a loot animation, treat the area as clear
        // so the loot sequence can continue uninterrupted.
        if (bb.Get<bool>("isLooting"))
            return NodeState.Success;

        // Return the cached result between scans.
        if (Time.time < _nextScanTime)
        {
            // Even if the last scan was clear, honour the grace period so a
            // brief enemy-detection gap doesn't immediately re-enable looting.
            if (!_cachedClear || Time.time < _clearAfter)
                return NodeState.Failure;
            return NodeState.Success;
        }

        _nextScanTime = Time.time + ScanInterval;

        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            var hp = enemyObj.GetComponent<HealthComponent>();
            if (hp != null && hp.currentHealth <= 0) continue;

            if (_fogManager != null && !_fogManager.IsRevealed(enemyObj.transform.position))
                continue;

            float dist = Vector3.Distance(self.position, enemyObj.transform.position);
            if (dist > _range) continue;

            if (_wallLayers != 0 &&
                !VisionUtilities.HasLineOfSight(self.position, enemyObj.transform.position, _wallLayers))
                continue;

            // Enemy confirmed — refresh the grace window.
            _cachedClear = false;
            _clearAfter  = Time.time + GracePeriod;
            return NodeState.Failure;
        }

        // No enemy found this scan.  Only report clear once the grace window
        // has fully elapsed so boundary flicker can't restart looting mid-twitch.
        if (Time.time < _clearAfter)
        {
            _cachedClear = false;
            return NodeState.Failure;
        }

        _cachedClear = true;
        return NodeState.Success;
    }
}