using UnityEngine;

/// <summary>
/// FUZZY CONDITION: Gates wary exploration using a two-factor fuzzy score.
///
/// PURPOSE
///   Rather than a hard "if HP &lt; X AND enemy visible → explore", this node
///   computes a continuous wary score from two fuzzy inputs and passes when that
///   score exceeds a threshold.  Both factors must be elevated simultaneously —
///   a healthy hero ignores nearby enemies; a dying hero with no enemies nearby
///   just waits for healing; only when BOTH hurt AND threatened does the hero
///   divert away from combat to seek safer ground.
///
/// FUZZY SCORE
///   fearScore   = RampDown(hpFraction,  loHP,          hiHP)
///               → 1 when nearly dead, 0 when healthy
///
///   threatScore = RampDown(nearestEnemyDist, nearDist, detectionRange)
///               → 1 when an enemy is immediately adjacent,
///                 0 when the enemy is at the edge of detection range
///
///   waryScore   = fearScore × threatScore   (fuzzy AND — both must be high)
///   Passes      → waryScore ≥ threshold
///
/// EXAMPLE (Knight defaults: loHP=0.20, hiHP=0.65, detectionRange=10, near=2)
///   HP 40%, enemy 3 u away:
///     fearScore  = RampDown(0.40, 0.20, 0.65) ≈ 0.56
///     threatScore= RampDown(3.0,  2.0,  10.0) ≈ 0.88
///     waryScore  = 0.56 × 0.88 = 0.49 — passes threshold 0.20 → Wary Explore
///
///   HP 80%, enemy 3 u away:
///     fearScore  = RampDown(0.80, 0.20, 0.65) = 0.0 (above hiHP)
///     waryScore  = 0.0  — does NOT pass → hero still attacks normally
///
///   HP 40%, no enemy visible:
///     threatScore = 0 (no enemy found within range)
///     waryScore   = 0  — does NOT pass → hero follows / heals normally
///
/// WHY NO BOOLEAN CONDITIONS
///   Binary "is enemy visible?" would cause the same flickering that
///   NoRevealedEnemies uses a grace period to suppress.  Here threatScore
///   degrades smoothly with distance, so an enemy at 9/10 of detection range
///   contributes only a weak threat — naturally less likely to divert the hero.
/// </summary>
public class WaryExploreGuard : Node
{
    // ── Fuzzy parameters ──────────────────────────────────────────────────────
    private readonly float _loHP;           // HP fraction where fear = 1
    private readonly float _hiHP;           // HP fraction where fear = 0
    private readonly float _nearDist;       // Enemy distance where threat = 1
    private readonly float _detectionRange; // Enemy distance where threat = 0
    private readonly float _threshold;      // waryScore needed to pass

    // ── Scan throttle ──────────────────────────────────────────────────────────
    // Enemy scan is shared with other nodes (FleeFromNearestEnemy, SelectCombatTarget)
    // so we throttle to avoid redundant FindGameObjectsWithTag calls.
    private const float ScanInterval = 0.25f;
    private float _nextScanTime   = float.MinValue;
    private float _cachedScore    = 0f;

    /// <param name="loHPFraction">   HP fraction at which fear reaches 1.</param>
    /// <param name="hiHPFraction">   HP fraction at which fear reaches 0 (fully calm).</param>
    /// <param name="nearDist">       Enemy distance at which threat score = 1.</param>
    /// <param name="detectionRange"> Enemy distance at which threat score = 0.</param>
    /// <param name="threshold">      Minimum waryScore (fearScore × threatScore) to pass.</param>
    public WaryExploreGuard(Blackboard bb,
                            float loHPFraction   = 0.20f,
                            float hiHPFraction   = 0.65f,
                            float nearDist       = 2.0f,
                            float detectionRange = 10f,
                            float threshold      = 0.20f) : base(bb)
    {
        _loHP           = loHPFraction;
        _hiHP           = hiHPFraction;
        _nearDist       = nearDist;
        _detectionRange = detectionRange;
        _threshold      = threshold;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var hc = self.GetComponent<HealthComponent>();
        if (hc == null) return NodeState.Failure;

        // ── Fear component (always fresh — HP changes continuously) ───────────
        float hpFraction = hc.maxHealth > 0f ? hc.currentHealth / hc.maxHealth : 0f;
        float fearScore  = FuzzyLogic.RampDown(hpFraction, _loHP, _hiHP);

        // Early-out: healthy hero, no fear → score is 0 regardless of enemies
        if (fearScore < 0.01f)
        {
            _cachedScore = 0f;
            return NodeState.Failure;
        }

        // ── Threat component (throttled scan) ─────────────────────────────────
        if (Time.time >= _nextScanTime)
        {
            _nextScanTime = Time.time + ScanInterval;
            float threatScore = ComputeThreatScore(self);
            _cachedScore = fearScore * threatScore;
        }
        else
        {
            // Re-apply current fearScore against cached threat each tick so
            // HP changes are still reflected even between enemy scans.
            float lastThreat = (_cachedScore > 0.001f && fearScore > 0.001f)
                               ? _cachedScore / fearScore
                               : 0f;
            _cachedScore = fearScore * lastThreat;
        }

        bool passes = _cachedScore >= _threshold;

        if (passes)
            UnityEngine.Debug.Log($"[WaryExploreGuard] {self.name} wary (score={_cachedScore:F2} " +
                                  $"≥ {_threshold}, HP {hpFraction:P0}) — diverting to safe fog");

        return passes ? NodeState.Success : NodeState.Failure;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the nearest living enemy within detection range and converts its
    /// distance to a threat score in [0, 1] using a fuzzy ramp.
    /// Returns 0 when no enemy is found.
    /// </summary>
    private float ComputeThreatScore(Transform self)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float bestDist = float.MaxValue;

        foreach (var e in enemies)
        {
            if (e == null) continue;
            var ehc = e.GetComponent<HealthComponent>();
            if (ehc != null && ehc.currentHealth <= 0f) continue;

            float dist = Vector2.Distance(self.position, e.transform.position);
            if (dist < bestDist)
                bestDist = dist;
        }

        if (bestDist >= _detectionRange)
            return 0f;   // no enemy in range — no threat

        return FuzzyLogic.RampDown(bestDist, _nearDist, _detectionRange);
    }
}
