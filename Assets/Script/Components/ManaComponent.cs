using UnityEngine;

/// <summary>
/// Manages a unit's mana pool. Mana regenerates slowly over time.
///
/// WHICH CLASSES USE MANA:
///   All heroes have a ManaComponent so the UI can always show a mana bar.
///   attackManaCost — set > 0 only on the Mage prefab; other classes leave it at 0.
///   healManaCost   — set > 0 only on the Healer prefab; other classes leave it at 0.
///   A cost of 0 means the action is free (the UseMana call is a no-op).
///
/// AUTO-POTION:
///   When mana drops below AutoPotionThreshold and a mana potion is in the inventory,
///   EquipmentComponent.Update() calls ConsumeManaPotion() which then calls AddMana().
/// </summary>
public class ManaComponent : MonoBehaviour
{
    /// <summary>
    /// Fired whenever mana changes (regen tick, UseMana, AddMana).
    /// Subscribe in UI to drive the mana bar.  Parameters: (component, current, max).
    /// </summary>
    public static event System.Action<ManaComponent, float, float> OnManaChanged;

    [Header("Mana Pool")]
    [SerializeField] private float _maxMana = 100f;

    [Header("Regeneration")]
    [Tooltip("Mana restored per second. Keep this very low — full bar should take ~50 seconds.")]
    [SerializeField] private float _regenRate = 2f;

    [Header("Action Costs (0 = free for this class)")]
    [Tooltip("Mana consumed each time this unit fires an attack. Set > 0 on the Mage prefab only.")]
    public float attackManaCost = 0f;

    [Tooltip("Mana consumed each time this unit casts a heal. Set > 0 on the Healer prefab only.")]
    public float healManaCost = 0f;

    public float maxMana     => _maxMana;
    public float currentMana { get; private set; }

    /// <summary>0–1 fraction of current / max mana.</summary>
    public float ManaFraction => _maxMana > 0f ? currentMana / _maxMana : 0f;

    private void Awake()
    {
        currentMana = _maxMana;
    }

    private void Update()
    {
        if (currentMana >= _maxMana) return;

        currentMana = Mathf.Min(currentMana + _regenRate * Time.deltaTime, _maxMana);
        OnManaChanged?.Invoke(this, currentMana, _maxMana);
    }

    /// <summary>
    /// Attempts to spend <paramref name="cost"/> mana.
    /// Returns true and deducts mana when successful.
    /// Returns false (without modifying mana) when insufficient.
    /// Always returns true when cost is 0 (free action).
    /// </summary>
    public bool UseMana(float cost)
    {
        if (cost <= 0f) return true;
        if (currentMana < cost) return false;

        currentMana -= cost;
        OnManaChanged?.Invoke(this, currentMana, _maxMana);
        return true;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> mana (e.g. from a mana potion). Clamped to maxMana.
    /// </summary>
    public void AddMana(float amount)
    {
        if (amount <= 0f) return;
        currentMana = Mathf.Min(currentMana + amount, _maxMana);
        OnManaChanged?.Invoke(this, currentMana, _maxMana);
        Debug.Log($"[ManaComponent] {gameObject.name} +{amount} mana → {currentMana:F0}/{_maxMana}");
    }
}
