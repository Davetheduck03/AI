using UnityEngine;

/// <summary>
/// Static utility for fuzzy membership functions and operators.
///
/// MEMBERSHIP FUNCTIONS — map a crisp input value to a 0–1 degree of membership.
///   Ramp      — linear rise from lo (=0) to hi (=1)
///   RampDown  — linear fall from lo (=1) to hi (=0)
///   Triangle  — peaks at mid, zero at lo and hi
///
/// FUZZY OPERATORS
///   And  — min(a, b)   both conditions must hold
///   Or   — max(a, b)   at least one condition holds
///   Not  — 1 - a       invert membership
///
/// USAGE EXAMPLES
///
///   // "How urgently does this ally need healing?"
///   float hpUrgency = FuzzyLogic.RampDown(hpFraction, 0f, 0.75f);
///
///   // "How willing is the Mage to engage in combat given current mana?"
///   float willingness = FuzzyLogic.Ramp(manaFraction, 0.15f, 0.55f);
///
///   // "Score = urgency AND proximity"
///   float score = FuzzyLogic.And(hpUrgency, FuzzyLogic.RampDown(dist, 0f, searchRange));
/// </summary>
public static class FuzzyLogic
{
    // ── Membership functions ──────────────────────────────────────────────────

    /// <summary>
    /// Linear ramp: 0 when value ≤ lo, 1 when value ≥ hi, linear in between.
    /// Use for "how much IS this property true?" (more value = more true).
    /// </summary>
    public static float Ramp(float value, float lo, float hi)
    {
        if (hi <= lo) return value >= hi ? 1f : 0f;
        return Mathf.Clamp01((value - lo) / (hi - lo));
    }

    /// <summary>
    /// Inverse ramp: 1 when value ≤ lo, 0 when value ≥ hi, linear in between.
    /// Use for "how much is this property NOT true?" (less value = more true).
    /// e.g. RampDown(hpFraction, 0f, 0.75f) → 1 at 0 HP, 0 at 75 % HP.
    /// </summary>
    public static float RampDown(float value, float lo, float hi)
        => 1f - Ramp(value, lo, hi);

    /// <summary>
    /// Triangular membership: 0 at lo, peaks at 1 at mid, 0 again at hi.
    /// Use for "how much is value in the sweet spot?"
    /// </summary>
    public static float Triangle(float value, float lo, float mid, float hi)
    {
        if (value <= lo || value >= hi) return 0f;
        return value < mid
            ? (value - lo)  / (mid - lo)
            : (hi  - value) / (hi  - mid);
    }

    // ── Fuzzy operators ───────────────────────────────────────────────────────

    /// <summary>Fuzzy AND — min(a, b). Both conditions must hold.</summary>
    public static float And(float a, float b) => Mathf.Min(a, b);

    /// <summary>Fuzzy OR — max(a, b). At least one condition must hold.</summary>
    public static float Or(float a, float b)  => Mathf.Max(a, b);

    /// <summary>Fuzzy NOT — 1 − a. Inverts membership degree.</summary>
    public static float Not(float a)           => 1f - a;
}
