using UnityEngine;

/// <summary>
/// Per-hero component that listens to the global DamageComponent.OnAttackFired and
/// HealthComponent.OnHealCast events and spawns the appropriate VFX when this hero
/// is the source.
///
/// Automatically added to every hero by BaseHero.Start().
///
/// Attack VFX:
///   Bow   → arrow projectile + impact burst (yellow/orange)
///   Staff → magic bolt projectile + AoE burst (purple)
///   Melee → no projectile VFX (effect is too fast to be meaningful)
///
/// Heal VFX:
///   Healer → green sparkles + ring pulse on the target
/// </summary>
public class HeroVFXController : MonoBehaviour
{
    // How much of the attack range to treat as the mage's AoE for the impact VFX.
    // The actual gameplay AoE is read from DamageComponent, but that value isn't
    // passed through the event.  A small visual radius looks fine regardless.
    private const float MageVfxAoeRadius = 1.4f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        DamageComponent.OnAttackFired += HandleAttackFired;
        HealthComponent.OnHealCast    += HandleHealCast;
    }

    private void OnDisable()
    {
        DamageComponent.OnAttackFired -= HandleAttackFired;
        HealthComponent.OnHealCast    -= HandleHealCast;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleAttackFired(
        GameObject  attacker,
        Vector3     attackerPos,
        Vector3     targetPos,
        WeaponType  weaponType,
        bool        isAoE)
    {
        // Only handle events that originated from this hero
        if (attacker != gameObject) return;

        switch (weaponType)
        {
            case WeaponType.Bow:
                VFXProjectile.SpawnArrow(attackerPos, targetPos);
                break;

            case WeaponType.Staff:
                VFXProjectile.SpawnMageBolt(attackerPos, targetPos, MageVfxAoeRadius);
                break;

            // Melee weapons: no projectile — the hit flash on the enemy is enough
            default:
                break;
        }
    }

    private void HandleHealCast(GameObject healer, GameObject target)
    {
        // Only handle events where this hero was the healer
        if (healer != gameObject) return;
        if (target == null) return;

        ParticleFactory.SpawnHealEffect(target.transform.position);
    }
}
