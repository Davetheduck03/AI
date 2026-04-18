using System;
using UnityEngine;

public class HealthComponent : UnitComponent
{
    /// <summary>Fired just before the owning GameObject is destroyed. Used by BaseHero to trigger the lose state.</summary>
    public static event Action<HealthComponent> OnDeath;

    /// <summary>
    /// Fired whenever a unit takes damage.
    /// Parameters: (damageDealer, finalDamage) — damageDealer is the source GameObject
    /// (may be null if the source is unknown), finalDamage is the post-armour amount.
    /// Consumed by DamageTracker to accumulate per-hero totals.
    /// </summary>
    public static event Action<GameObject, float> OnDamageDealt;

    /// <summary>
    /// Fired whenever this unit is healed.
    /// Parameters: (healer, healTarget) — healer may be null if the source is unknown.
    /// Consumed by HeroVFXController to play heal particle effects.
    /// </summary>
    public static event Action<GameObject, GameObject> OnHealCast;

    // Optional flash effect — present on units that have a SpriteRenderer.
    private HitFlashEffect _flash;

    public float currentHealth { get; private set; }
    public float maxHealth { get; private set; }
    public bool isDamagable { get; private set; }

    // Flat armor from base stats + equipment
    public float totalArmor => baseArmor + armorBonus;

    // Derived from armor: armor / (armor + 100)
    // e.g. 50 armor = 33% reduction, 100 armor = 50%, 200 armor = 67%
    public float DamageReduction => totalArmor / (totalArmor + 100f);

    private float baseArmor;
    private float armorBonus = 0f;

    protected override void OnInitialize()
    {
        baseArmor     = data.armor;
        maxHealth     = data.Health;
        currentHealth = maxHealth;
        isDamagable   = true;

        // Grab the flash effect if this unit has one (not mandatory).
        _flash = GetComponent<HitFlashEffect>();
    }

    public void AddArmorBonus(float amount)
    {
        armorBonus += amount;
        Debug.Log($"[HealthComponent] {gameObject.name} armor: {totalArmor} " +
                  $"({DamageReduction:P0} reduction)");
    }

    public void AddMaxHealthBonus(float amount)
    {
        maxHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth + amount, 1f, maxHealth);
    }

    public void TakeDamage(DamageData data)
    {
        if (!isDamagable) return;

        float finalDamage = data.amount * (1f - DamageReduction);
        currentHealth -= finalDamage;

        Debug.Log($"[HealthComponent] {gameObject.name} took {finalDamage:F1} damage " +
                  $"({data.amount:F1} raw, {DamageReduction:P0} reduced). " +
                  $"HP: {currentHealth:F1}/{maxHealth}");

        _flash?.FlashDamage();

        // Notify the damage tracker (and any other listeners) about this hit.
        OnDamageDealt?.Invoke(data.damageDealer, finalDamage);

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>
    /// Restores <paramref name="amount"/> HP, capped at max health.
    /// Pass <paramref name="healer"/> so VFX listeners know the source.
    /// </summary>
    public void Heal(float amount, GameObject healer = null)
    {
        if (amount <= 0f) return;
        float before = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"[HealthComponent] {gameObject.name} healed {currentHealth - before:F1} HP. " +
                  $"HP: {currentHealth:F1}/{maxHealth}");

        _flash?.FlashHeal();

        // Notify VFX listeners
        OnHealCast?.Invoke(healer, gameObject);
    }

    /// <summary>Resets health to max and re-enables damage. Used when the hero is respawned.</summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDamagable   = true;
    }

    private void Die()
    {
        // Drop the relic (if held) before the GameObject is destroyed so it spawns as a world item.
        GetComponent<EquipmentComponent>()?.DropOnDeath();

        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }
}