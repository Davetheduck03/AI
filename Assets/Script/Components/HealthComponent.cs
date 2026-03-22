using System;
using UnityEngine;

public class HealthComponent : UnitComponent
{
    /// <summary>Fired just before the owning GameObject is destroyed. Used by BaseHero to trigger the lose state.</summary>
    public static event Action<HealthComponent> OnDeath;

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
        baseArmor = data.armor;
        maxHealth = data.Health;
        currentHealth = maxHealth;
        isDamagable = true;
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

        if (currentHealth <= 0)
            Die();
    }

    /// <summary>Resets health to max and re-enables damage. Used when the hero is respawned.</summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDamagable   = true;
    }

    private void Die()
    {
        OnDeath?.Invoke(this);
        Destroy(gameObject);
    }
}