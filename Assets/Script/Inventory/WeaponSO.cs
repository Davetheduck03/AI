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

    public override float GetScore()
    {
        return damageBonus + (attackSpeedBonus * 0.5f);
    }
}