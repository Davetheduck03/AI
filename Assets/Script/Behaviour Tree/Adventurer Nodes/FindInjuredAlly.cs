using UnityEngine;

/// <summary>
/// CONDITION/ACTION: Scans teammates for an ally below <paramref name="healthThreshold"/>
/// and writes the highest-urgency one's Transform to bb[<paramref name="targetKey"/>].
///
/// TARGET SELECTION — FUZZY URGENCY
///   Previously the node picked the ally with the lowest raw HP fraction.
///   This caused the healer to chase a 60 % ally across the map while ignoring
///   a 50 % ally standing right next to it (because 50 > 60 raw, but the nearby
///   ally is a far better use of a cast).
///
///   Now each candidate gets a fuzzy urgency score that combines:
///     hpUrgency  — RampDown(hpFraction, 0, threshold)  → 1 near death, 0 at threshold
///     proximity  — RampDown(distance,   0, searchRange) → 1 at self, 0 at edge of range
///     score      — And(hpUrgency, proximity)            → both must be high to win
///
///   This means a critically wounded ally nearby almost always wins, but a
///   far-away ally at 5 % HP still beats a nearby ally at 60 % HP because
///   hpUrgency(0.05) = ~0.93 beats hpUrgency(0.60) = ~0.20 even after the
///   distance penalty.
///
/// Returns Success when an injured ally is found.
/// Returns Failure when everyone is healthy (or no allies are in range).
/// </summary>
public class FindInjuredAlly : Node
{
    private readonly float  _healthThreshold;
    private readonly float  _searchRange;
    private readonly string _targetKey;
    private readonly bool   _includeSelf;

    /// <param name="healthThreshold">HP fraction (0–1) at or below which a hero needs healing.</param>
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

        Transform bestTarget   = null;
        float     bestUrgency  = -1f;

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

            // ── Fuzzy urgency score ───────────────────────────────────────────
            // hpUrgency:  approaches 1 as HP → 0, reaches 0 at threshold
            // proximity:  approaches 1 as distance → 0, reaches 0 at searchRange
            // Combined via AND (min) so BOTH being high is required for high score.
            float hpUrgency = FuzzyLogic.RampDown(fraction, 0f, _healthThreshold);
            float proximity = FuzzyLogic.RampDown(dist, 0f, _searchRange);
            float urgency   = FuzzyLogic.And(hpUrgency, proximity);

            if (urgency > bestUrgency)
            {
                bestUrgency = urgency;
                bestTarget  = p.transform;
            }
        }

        if (bestTarget == null)
        {
            bb.Set<Transform>(_targetKey, null);
            return NodeState.Failure;
        }

        bb.Set(_targetKey, bestTarget);
        Debug.Log($"[FindInjuredAlly] {self.name} targeting {bestTarget.name} " +
                  $"(urgency {bestUrgency:F2})");
        return NodeState.Success;
    }
}
