using UnityEngine;

/// <summary>
/// CONDITION: Returns Success if NO actionable enemy is in range.
///
/// "Actionable" matches the same criteria used by FindNearestRevealedEnemy:
///   • Not in fog of war
///   • Within <paramref name="range"/>
///   • Has line-of-sight (when <paramref name="wallLayers"/> is non-zero)
///
/// Passing wallLayers here keeps this guard consistent with the attack
/// sequence — an enemy behind a wall no longer blocks looting/following.
/// </summary>
public class NoRevealedEnemies : Node
{
    private readonly float      _range;
    private readonly LayerMask  _wallLayers;
    private FogOfWarManager     _fogManager;

    // Throttle the enemy scan — FindGameObjectsWithTag is expensive and this
    // node runs twice per hero per BT tick (loot guard + world-item guard).
    private const float ScanInterval = 0.2f;
    private float       _nextScanTime = float.MinValue;
    private bool        _cachedClear  = true;   // true = no threat found

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

        // Return the cached result between scans to avoid calling
        // FindGameObjectsWithTag + LOS raycasts every tick.
        if (Time.time < _nextScanTime)
            return _cachedClear ? NodeState.Success : NodeState.Failure;

        _nextScanTime = Time.time + ScanInterval;

        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            // Skip dead enemies — they may retain the "Enemy" tag during a death
            // animation before the GameObject is destroyed.
            var hp = enemyObj.GetComponent<HealthComponent>();
            if (hp != null && hp.currentHealth <= 0) continue;

            // Skip enemies still in fog.
            if (_fogManager != null && !_fogManager.IsRevealed(enemyObj.transform.position))
                continue;

            float dist = Vector3.Distance(self.position, enemyObj.transform.position);
            if (dist > _range) continue;

            // Skip enemies behind walls.
            if (_wallLayers != 0 &&
                !VisionUtilities.HasLineOfSight(self.position, enemyObj.transform.position, _wallLayers))
                continue;

            _cachedClear = false;
            return NodeState.Failure;
        }

        _cachedClear = true;
        return NodeState.Success;
    }
}