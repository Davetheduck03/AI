/// <summary>
/// CONDITION: Gates combat initiation on fuzzy HP willingness.
///
/// Returns Success  — hero is healthy enough to want to fight.
/// Returns Failure  — hero is too hurt to proactively engage; BT falls through
///                    to Follow/Explore so they stay near the healer until topped up.
///
/// HOW IT WORKS
///   willingness = Ramp(hpFraction, loHP, hiHP)
///   Passes when willingness ≥ threshold.
///
///   e.g. Knight (lo=0.20, hi=0.60, threshold=0.35):
///     100 % HP → willingness 1.0 → attacks freely
///      40 % HP → willingness 0.5 → still attacks (0.5 ≥ 0.35)
///      30 % HP → willingness 0.25 → retreats     (0.25 < 0.35)
///      20 % HP → willingness 0.0 → retreats hard
///
/// IMPORTANT — SEQUENCE NON-REACTIVITY
///   The Sequence node holds currentIndex while Running, so once AdaptiveAttack
///   returns Running the guard is bypassed on every subsequent tick.  This means
///   FuzzyHPGuard only blocks the START of a new engagement — a hero already in
///   combat will always finish fighting the current enemy before retreating.
///   This is intentional: heroes don't run mid-swing, they disengage cleanly.
///
/// CLASS PERSONALITIES (suggested Inspector values)
///   Knight  — lo 0.20  hi 0.60  threshold 0.35  (fights hard, retreats ~35 % HP)
///   Archer  — lo 0.30  hi 0.70  threshold 0.45  (cautious, retreats ~48 % HP)
///   Paladin — lo 0.25  hi 0.65  threshold 0.40  (moderate)
///   Mage    — lo 0.25  hi 0.60  threshold 0.40  (fragile; stacks with FuzzyManaGuard)
/// </summary>
public class FuzzyHPGuard : Node
{
    private readonly float _loHP;
    private readonly float _hiHP;
    private readonly float _threshold;

    /// <param name="loHPFraction">HP fraction at which willingness reaches 0 (fully reluctant).</param>
    /// <param name="hiHPFraction">HP fraction at which willingness reaches 1 (fully willing).</param>
    /// <param name="threshold">Minimum willingness score required to pass.</param>
    public FuzzyHPGuard(Blackboard bb,
                        float loHPFraction = 0.25f,
                        float hiHPFraction = 0.65f,
                        float threshold    = 0.40f) : base(bb)
    {
        _loHP      = loHPFraction;
        _hiHP      = hiHPFraction;
        _threshold = threshold;
    }

    public override NodeState Evaluate()
    {
        var self = bb.Get<UnityEngine.Transform>("self");
        if (self == null) return NodeState.Success;

        var hc = self.GetComponent<HealthComponent>();
        if (hc == null) return NodeState.Success;   // no HP component — don't block

        float hpFraction  = hc.currentHealth / hc.maxHealth;
        float willingness = FuzzyLogic.Ramp(hpFraction, _loHP, _hiHP);
        bool  passes      = willingness >= _threshold;

        if (!passes)
            UnityEngine.Debug.Log($"[FuzzyHPGuard] {self.name} — willingness {willingness:F2} < {_threshold} " +
                                  $"(HP {hc.currentHealth:F0}/{hc.maxHealth:F0}) — retreating to healer");

        return passes ? NodeState.Success : NodeState.Failure;
    }
}
