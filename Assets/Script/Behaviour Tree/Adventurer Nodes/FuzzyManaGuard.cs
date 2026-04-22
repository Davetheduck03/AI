using UnityEngine;

/// <summary>
/// CONDITION: Gates combat on mana willingness using a fuzzy ramp.
///
/// Returns Success  when the unit has enough mana to confidently engage.
/// Returns Failure  when mana is so low that engaging is not worthwhile —
///                  allowing the BT to fall through to Follow/Explore until
///                  mana regenerates or a mana potion is consumed.
///
/// Willingness is a 0–1 fuzzy score:
///   mana ≤ loManaFraction  → willingness 0   (too depleted to fight)
///   mana ≥ hiManaFraction  → willingness 1   (fully ready)
///   between                → linear ramp
///
/// The guard passes when willingness ≥ <see cref="_threshold"/>.
///
/// INTENDED USE: Mage attack sequence.
///   attackSeq.AddChild(new FuzzyManaGuard(bb));
///   If the Mage is below ~15 % mana it stops engaging and waits for regen,
///   making mana management a real tactical resource rather than a hard wall.
///
/// NOTE: Units without a ManaComponent (or with attackManaCost == 0) are
/// considered always willing — the guard passes unconditionally for them.
/// </summary>
public class FuzzyManaGuard : Node
{
    private readonly float _loMana;     // mana fraction below which willingness = 0
    private readonly float _hiMana;     // mana fraction above which willingness = 1
    private readonly float _threshold;  // minimum willingness to pass

    /// <param name="loManaFraction">Mana fraction at which the Mage refuses to engage (default 15 %).</param>
    /// <param name="hiManaFraction">Mana fraction at which the Mage is fully willing (default 55 %).</param>
    /// <param name="threshold">Minimum fuzzy willingness score required to pass (default 0.4).</param>
    public FuzzyManaGuard(Blackboard bb,
                          float loManaFraction = 0.15f,
                          float hiManaFraction = 0.55f,
                          float threshold      = 0.40f) : base(bb)
    {
        _loMana    = loManaFraction;
        _hiMana    = hiManaFraction;
        _threshold = threshold;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Success;   // no self — don't block

        var mana = self.GetComponent<ManaComponent>();

        // No mana component, or action is free (cost 0) — always willing
        if (mana == null || mana.attackManaCost <= 0f)
            return NodeState.Success;

        float willingness = FuzzyLogic.Ramp(mana.ManaFraction, _loMana, _hiMana);

        bool passes = willingness >= _threshold;
        if (!passes)
            Debug.Log($"[FuzzyManaGuard] {self.name} — willingness {willingness:F2} < {_threshold} " +
                      $"(mana {mana.currentMana:F0}/{mana.maxMana}) — skipping combat");

        return passes ? NodeState.Success : NodeState.Failure;
    }
}
