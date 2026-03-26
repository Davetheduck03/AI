using UnityEngine;

/// <summary>
/// CONDITION/ACTION: Scans teammates for an ally below <paramref name="healthThreshold"/>
/// and writes the most injured one's Transform to bb[<paramref name="targetKey"/>].
///
/// Returns Success when an injured ally is found.
/// Returns Failure when everyone is healthy (or no allies are in range).
///
/// Heal-target key defaults to "healTarget" so it never collides with bb["target"]
/// used by the combat sequences.
/// </summary>
public class FindInjuredAlly : Node
{
    private readonly float  _healthThreshold;   // 0–1 fraction below which a heal is needed
    private readonly float  _searchRange;
    private readonly string _targetKey;
    private readonly bool   _includeSelf;

    /// <param name="healthThreshold">HP fraction (0–1) at or below which a hero needs healing.
    ///   e.g. 0.7 = heal anyone below 70 % health.</param>
    /// <param name="searchRange">World-unit radius to scan for injured allies.</param>
    /// <param name="targetKey">Blackboard key to write the chosen ally's Transform into.</param>
    /// <param name="includeSelf">If true, the caster can target themselves when injured.</param>
    public FindInjuredAlly(Blackboard bb,
                           float healthThreshold = 0.7f,
                           float searchRange     = 12f,
                           string targetKey      = "healTarget",
                           bool includeSelf      = false) : base(bb)
    {
        _healthThreshold = healthThreshold;
        _searchRange     = searchRange;
        _targetKey       = targetKey;
        _includeSelf     = includeSelf;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        Transform mostInjured      = null;
        float     lowestHPFraction = float.MaxValue;

        foreach (GameObject p in players)
        {
            if (p == null) continue;
            if (!_includeSelf && p.transform == self) continue;

            float dist = Vector3.Distance(self.position, p.transform.position);
            if (dist > _searchRange) continue;

            var hc = p.GetComponent<HealthComponent>();
            if (hc == null) continue;

            float fraction = hc.currentHealth / hc.maxHealth;
            if (fraction >= _healthThreshold) continue;   // healthy enough — skip

            if (fraction < lowestHPFraction)
            {
                lowestHPFraction = fraction;
                mostInjured      = p.transform;
            }
        }

        if (mostInjured == null)
        {
            bb.Set<Transform>(_targetKey, null);
            return NodeState.Failure;
        }

        bb.Set(_targetKey, mostInjured);
        Debug.Log($"[FindInjuredAlly] {self.name} targeting {mostInjured.name} " +
                  $"({lowestHPFraction:P0} HP)");
        return NodeState.Success;
    }
}
