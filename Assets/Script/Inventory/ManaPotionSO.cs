using UnityEngine;

/// <summary>
/// A stackable consumable that restores mana.
/// Picked up on contact (like relics) — not via the behaviour-tree item system.
/// Auto-consumed by EquipmentComponent when the unit's mana drops below 20 %.
/// </summary>
[CreateAssetMenu(fileName = "ManaPotionSO", menuName = "Inventory/ManaPotionSO")]
public class ManaPotionSO : PotionSO
{
    [Header("Effect")]
    [Tooltip("Flat mana restored when this potion is consumed.")]
    public float manaAmount = 50f;

    /// <summary>Score used by EvaluateNearbyItems — higher manaAmount = higher priority.</summary>
    public override float GetScore() => manaAmount;
}
