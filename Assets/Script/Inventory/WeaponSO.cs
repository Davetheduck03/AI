using UnityEngine;

public enum WeaponType
{
    Sword,
    LongSword,
    Staff,
    Dagger,
    Bow,
}

[CreateAssetMenu(fileName = "WeaponSO", menuName = "Equipment/WeaponSO")]
public class WeaponSO : ItemSO
{
    [Header("Weapon Type")]
    public WeaponType weaponType;

    [Header("Stat Bonuses (added on top of unit base stats)")]
    public float damageBonus;
    public float attackSpeedBonus;  // Added to unit's baseAttackSpeed

    [Header("Healing (Staff only)")]
    [Tooltip("Extra flat healing added on top of the caster's TotalDamage when a Healer casts HealTarget.\n" +
             "Has no effect on non-Staff weapons or on attack damage.\n" +
             "Example: Staff with damageBonus=5 healingBonus=8 → Mage attacks for base+5, Healer heals for base+5+8.")]
    public float healingBonus;

    [Header("Range")]
    [Tooltip("Attack range for this weapon. 0 = use the hero's base range from HeroSO.\n" +
             "Balanced tiers: Bow 4.5 | Staff 3.5 | LongSword 3.0 | Sword 2.5 | Dagger 1.5")]
    public float range;

    /// <summary>
    /// Composite score used by EquipmentComponent to decide whether to pick up this weapon.
    ///   damage      — primary power stat (weight 1.0)
    ///   attackSpeed — affects DPS          (weight 0.5, half of damage to avoid speed dominating)
    ///   range       — affects safety/kiting (weight 0.2, tiebreaker between otherwise equal weapons)
    /// </summary>
    public override float GetScore()
    {
        return damageBonus + (attackSpeedBonus * 0.5f) + (range * 0.2f);
    }
}