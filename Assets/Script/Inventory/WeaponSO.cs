using UnityEngine;

public enum WeaponType
{
    Sword,       // Knight, Paladin
    LongSword,   // Knight, Paladin
    Staff,       // Mage, Healer
    Dagger,      // All classes
    Bow,         // Ranger (future)
}

[CreateAssetMenu(fileName = "WeaponSO", menuName = "Equipment/WeaponSO")]
public class WeaponSO : ItemSO
{
    [Header("Weapon Stats")]
    public WeaponType weaponType;
    public float attackDamageValue;
    public float attackSpeedValue;

    // Score = damage weighted + slight bonus for speed
    public override float GetScore()
    {
        return attackDamageValue + (attackSpeedValue * 0.5f);
    }
}