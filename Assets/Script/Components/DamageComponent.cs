using System;
using UnityEngine;

/// <summary>
/// Tracks total damage, attack speed, range, and AoE.
/// Base values come from UnitSO/HeroSO. Weapons add bonuses on top.
/// </summary>
public class DamageComponent : UnitComponent
{
    /// <summary>
    /// Fired whenever this component lands an attack.
    /// Parameters: (attacker, attackerPosition, targetPosition, weaponType, isAoE)
    /// Consumed by HeroVFXController to spawn projectile / impact VFX.
    /// </summary>
    public static event Action<GameObject, Vector3, Vector3, WeaponType, bool> OnAttackFired;

    // ── Damage ────────────────────────────────────
    private float baseDamage;
    private float damageBonus = 0f;
    public float TotalDamage => baseDamage + damageBonus;

    // ── Attack Speed ──────────────────────────────
    private float baseAttackSpeed;
    private float attackSpeedBonus = 0f;
    public float TotalAttackSpeed => Mathf.Max(0.1f, baseAttackSpeed + attackSpeedBonus);
    public float AttackCooldown => 1f / TotalAttackSpeed;

    // ── Range & AoE ───────────────────────────────
    private float baseRange;
    public float AttackRange { get; private set; }
    public bool IsAoE { get; private set; }

    protected override void OnInitialize()
    {
        baseDamage = data.baseDamage;
        baseAttackSpeed = data.baseAttackSpeed;

        // Range and AoE only exist on HeroSO — enemies fall back to defaults
        if (data is HeroSO heroData)
        {
            baseRange   = heroData.range;
            AttackRange = heroData.range;
            IsAoE       = heroData.isAoE;
        }
        else if (data is EnemySO enemyData)
        {
            baseRange   = enemyData.range;
            AttackRange = enemyData.range;
        }

        Debug.Log($"[DamageComponent] {gameObject.name} initialized — " +
                  $"Damage: {TotalDamage}, Speed: {TotalAttackSpeed}/s, " +
                  $"Range: {AttackRange}, AoE: {IsAoE}");
    }

    // ── Stat modifiers ────────────────────────────

    /// <summary>
    /// Scales baseDamage by <paramref name="multiplier"/>.
    /// Called by DungeonSpawner after enemy instantiation to apply floor difficulty scaling.
    /// e.g. multiplier = 1.3 on floor 3 → enemy deals 30 % more damage than baseline.
    /// </summary>
    public void ScaleDamage(float multiplier)
    {
        if (multiplier <= 0f) return;
        baseDamage *= multiplier;
        Debug.Log($"[DamageComponent] {gameObject.name} damage scaled ×{multiplier:F2} → {TotalDamage:F1} total");
    }

    public void AddDamageBonus(float amount)
    {
        damageBonus += amount;
        Debug.Log($"[DamageComponent] {gameObject.name} damage: " +
                  $"{baseDamage} base + {damageBonus} bonus = {TotalDamage} total");
    }

    public void AddAttackSpeedBonus(float amount)
    {
        attackSpeedBonus += amount;
        Debug.Log($"[DamageComponent] {gameObject.name} attack speed: " +
                  $"{baseAttackSpeed} base + {attackSpeedBonus} bonus = {TotalAttackSpeed}/s " +
                  $"(cooldown: {AttackCooldown:F2}s)");
    }

    /// <summary>
    /// Called by EquipmentComponent when a weapon is equipped or unequipped.
    /// Pass the weapon's range to override; pass 0 to fall back to the hero's base range.
    /// </summary>
    public void SetWeaponRange(float weaponRange)
    {
        AttackRange = weaponRange > 0f ? weaponRange : baseRange;
        Debug.Log($"[DamageComponent] {gameObject.name} attack range → {AttackRange:F2} " +
                  $"({(weaponRange > 0f ? "weapon override" : "hero base")})");
    }

    // ── Deal damage ───────────────────────────────

    /// <summary>
    /// Full overload: uses AoE or single-target logic automatically.
    /// Pass selfPosition and targetLayer for AoE; they're ignored for single-target.
    /// </summary>
    public void TryDealDamage(GameObject primaryTarget, Vector2 selfPosition, LayerMask targetLayer)
    {
        if (IsAoE)
            DealAoeDamage(selfPosition, targetLayer);
        else
            DealSingleDamage(primaryTarget);
    }

    /// <summary>
    /// Simple overload for enemies: always single-target, no position needed.
    /// </summary>
    public void TryDealDamage(GameObject target)
    {
        DealSingleDamage(target);
    }

    // ── Internal helpers ──────────────────────────

    /// <summary>
    /// Reads the currently equipped weapon type from EquipmentComponent.
    /// Returns WeaponType.Sword as the melee default when no ranged weapon is found.
    /// </summary>
    private WeaponType GetCurrentWeaponType()
    {
        var eq = GetComponent<EquipmentComponent>();
        if (eq?.equippedWeapon != null)
            return eq.equippedWeapon.weaponType;
        return WeaponType.Sword;
    }

    private void DealSingleDamage(GameObject target)
    {
        if (target == null) return;

        if (target.TryGetComponent<HealthComponent>(out var health) && health.isDamagable)
        {
            // Notify VFX listeners before damage lands so projectile launches immediately
            OnAttackFired?.Invoke(gameObject, transform.position,
                                  target.transform.position, GetCurrentWeaponType(), false);

            health.TakeDamage(new DamageData(TotalDamage, gameObject));
            Debug.Log($"[DamageComponent] {gameObject.name} → {target.name} " +
                      $"for {TotalDamage:F1} dmg");
        }
    }

    private void DealAoeDamage(Vector2 origin, LayerMask targetLayer)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, AttackRange, targetLayer);

        int hitCount = 0;
        GameObject firstTarget = null;
        foreach (Collider2D col in hits)
        {
            if (col.gameObject == gameObject) continue;     // don't hit self

            if (col.TryGetComponent<HealthComponent>(out var health) && health.isDamagable)
            {
                if (firstTarget == null)
                    firstTarget = col.gameObject;
                health.TakeDamage(new DamageData(TotalDamage, gameObject));
                hitCount++;
            }
        }

        // Notify VFX listeners once for the AoE burst (aim at primary target if any)
        if (hitCount > 0 && firstTarget != null)
        {
            OnAttackFired?.Invoke(gameObject, transform.position,
                                  firstTarget.transform.position, GetCurrentWeaponType(), true);
        }

        Debug.Log($"[DamageComponent] {gameObject.name} AoE hit {hitCount} target(s) " +
                  $"for {TotalDamage:F1} dmg (radius: {AttackRange})");
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !IsAoE) return;

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}