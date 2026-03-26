using UnityEngine;
using TMPro;

/// <summary>
/// Displays one hero's player label and cumulative damage dealt for the current run.
///
/// DamageTrackerPanelUI creates or activates one of these per hero after spawn and
/// calls SetHero() to bind it. The row then polls DamageTracker.Instance each Update()
/// so the figure stays live without needing additional events.
///
/// HOW TO SET UP IN THE EDITOR:
///   Create a horizontal child LayoutGroup inside the DamageTrackerPanel with:
///     • A TextMeshProUGUI for the player label (e.g. "Player 1")
///     • A TextMeshProUGUI for the damage total (right-aligned)
///   Drag both into the serialised fields below, then assign this component.
/// </summary>
public class HeroDamageRowUI : MonoBehaviour
{
    [Header("Row UI Elements")]
    [Tooltip("Shows 'Player 1', 'Player 2', etc.")]
    [SerializeField] private TextMeshProUGUI playerLabel;

    [Tooltip("Shows the running damage total, e.g. '1 234'.")]
    [SerializeField] private TextMeshProUGUI damageLabel;

    // Bound hero
    private GameObject _hero;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Binds this row to <paramref name="hero"/> (index is 0-based).
    /// Called by DamageTrackerPanelUI after heroes spawn.
    /// </summary>
    public void SetHero(GameObject hero, int index)
    {
        _hero = hero;

        if (playerLabel != null)
            playerLabel.text = $"Player {index + 1}";

        RefreshDamage();
    }

    // ── Unity Lifecycle ────────────────────────────────────────────────────────

    private void Update()
    {
        if (_hero == null) return;
        RefreshDamage();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void RefreshDamage()
    {
        if (damageLabel == null) return;
        float total = DamageTracker.Instance != null ? DamageTracker.Instance.GetTotal(_hero) : 0f;
        // Format with thousand separators so large numbers stay readable, e.g. "12 345"
        damageLabel.text = total.ToString("N0");
    }
}
