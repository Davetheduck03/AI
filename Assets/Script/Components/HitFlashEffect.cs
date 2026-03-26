using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a quick colour flash on this unit's SpriteRenderer.
///
///   Red  flash — unit took damage  (called from HealthComponent.TakeDamage)
///   Green flash — unit was healed  (called from HealthComponent.Heal)
///
/// The flash lerps from the tint colour back to the original colour over
/// <see cref="flashDuration"/> seconds. A new flash always cancels the
/// previous one so rapid hits don't stack into permanent tinting.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HitFlashEffect : MonoBehaviour
{
    [Tooltip("Duration in seconds for the flash to fade back to the original colour.")]
    [SerializeField] private float flashDuration = 0.2f;

    [Tooltip("Peak colour applied when the unit takes damage.")]
    [SerializeField] private Color damageColour = new Color(1f, 0.1f, 0.1f);   // bright red

    [Tooltip("Peak colour applied when the unit is healed.")]
    [SerializeField] private Color healColour = new Color(0.1f, 1f, 0.3f);      // bright green

    private SpriteRenderer _sr;
    private Color           _originalColour;
    private Coroutine       _activeFlash;

    private void Awake()
    {
        _sr            = GetComponent<SpriteRenderer>();
        _originalColour = _sr.color;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Flashes the sprite red. Call this when the unit takes damage.</summary>
    public void FlashDamage() => TriggerFlash(damageColour);

    /// <summary>Flashes the sprite green. Call this when the unit is healed.</summary>
    public void FlashHeal()   => TriggerFlash(healColour);

    // ── Internal ──────────────────────────────────────────────────────────────

    private void TriggerFlash(Color peakColour)
    {
        if (_activeFlash != null)
            StopCoroutine(_activeFlash);

        _activeFlash = StartCoroutine(FlashRoutine(peakColour));
    }

    private IEnumerator FlashRoutine(Color peakColour)
    {
        // Snap to peak immediately so the hit is visible every frame.
        _sr.color = peakColour;

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed     += Time.deltaTime;
            _sr.color    = Color.Lerp(peakColour, _originalColour, elapsed / flashDuration);
            yield return null;
        }

        _sr.color    = _originalColour;
        _activeFlash = null;
    }
}
