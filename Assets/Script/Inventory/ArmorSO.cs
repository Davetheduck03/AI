using UnityEngine;

/// <summary>
/// Shared base for all armor items (head, body, etc.).
/// Keeps statValue out of WeaponSO's Inspector — weapons use damageBonus / attackSpeedBonus instead.
/// </summary>
public abstract class ArmorSO : ItemSO
{
    [Header("Armor")]
    [Tooltip("Flat armor value added to the unit's total damage reduction.")]
    public float statValue;

    public override float GetScore() => statValue;
}
