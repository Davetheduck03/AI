using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines an adventurer class (Knight, Paladin, Mage, Healer, etc.)
/// and which weapon types they are allowed to equip.
/// Create via: Assets > Create > AI > Units > Adventurer Class
/// </summary>
[CreateAssetMenu(fileName = "New Adventurer Class", menuName = "AI/Units/Adventurer Class")]
public class AdventurerClassSO : ScriptableObject
{
    [Header("Class Info")]
    public string className;
    public Sprite classIcon;

    [Header("Allowed Weapon Types")]
    [Tooltip("Only weapons with these types can be equipped by this class.")]
    public List<WeaponType> allowedWeaponTypes = new List<WeaponType>();

    public bool CanEquipWeapon(WeaponSO weapon)
    {
        if (weapon == null) return false;
        return allowedWeaponTypes.Contains(weapon.weaponType);
    }
}
