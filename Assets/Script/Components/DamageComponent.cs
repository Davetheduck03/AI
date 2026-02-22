using UnityEngine;

/// <summary>
/// Tracks total damage, attack speed, range, and AoE.
/// Base values come from UnitSO/HeroSO. Weapons add bonuses on top.
/// </summary>
public class DamageComponent : UnitComponent
{
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
    public float AttackRange { get; private set; }
    public bool IsAoE { get; private set; }

    protected override void OnInitialize()
    {
        baseDamage = data.baseDamage;
        baseAttackSpeed = data.baseAttackSpeed;

        // Range and AoE only exist on HeroSO — enemies fall back to defaults
        if (data is HeroSO heroData)
        {
            AttackRange = heroData.range;
            IsAoE = heroData.isAoE;
        }
        else if (data is EnemySO enemyData)
        {
            AttackRange = enemyData.range;
        }

        Debug.Log($"[DamageComponent] {gameObject.name} initialized — " +
                  $"Damage: {TotalDamage}, Speed: {TotalAttackSpeed}/s, " +
                  $"Range: {AttackRange}, AoE: {IsAoE}");
    }

    // ── Stat modifiers ────────────────────────────

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

    private void DealSingleDamage(GameObject target)
    {
        if (target == null) return;

        if (target.TryGetComponent<HealthComponent>(out var health) && health.isDamagable)
        {
            health.TakeDamage(new DamageData(TotalDamage, gameObject));
            Debug.Log($"[DamageComponent] {gameObject.name} → {target.name} " +
                      $"for {TotalDamage:F1} dmg");
        }
    }

    private void DealAoeDamage(Vector2 origin, LayerMask targetLayer)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, AttackRange, targetLayer);

        int hitCount = 0;
        foreach (Collider2D col in hits)
        {
            if (col.gameObject == gameObject) continue;     // don't hit self

            if (col.TryGetComponent<HealthComponent>(out var health) && health.isDamagable)
            {
                health.TakeDamage(new DamageData(TotalDamage, gameObject));
                hitCount++;
            }
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