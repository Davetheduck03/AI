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

        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            // Skip enemies still in fog
            if (_fogManager != null && !_fogManager.IsRevealed(enemyObj.transform.position))
                continue;

            float dist = Vector3.Distance(self.position, enemyObj.transform.position);
            if (dist > _range) continue;

            // Skip enemies behind walls — consistent with FindNearestRevealedEnemy
            if (_wallLayers != 0 &&
                !VisionUtilities.HasLineOfSight(self.position, enemyObj.transform.position, _wallLayers))
                continue;

            Debug.Log($"[NoRevealedEnemies] {enemyObj.name} at {dist:F1} blocking non-combat actions");
            return NodeState.Failure;
        }

        return NodeState.Success;
    }
}