using UnityEngine;

/// <summary>
/// A stackable consumable that restores HP.
/// Picked up on contact (like relics) — not via the behaviour-tree item system.
/// Auto-consumed by EquipmentComponent when the hero's HP drops below 30 %.
/// </summary>
[CreateAssetMenu(fileName = "HealthPotionSO", menuName = "Inventory/HealthPotionSO")]
public class HealthPotionSO : PotionSO
{
    [Header("Effect")]
    [Tooltip("Flat HP restored when this potion is consumed.")]
    public float healAmount = 40f;

    /// <summary>Score used by EvaluateNearbyItems — higher healAmount = higher priority.</summary>
    public override float GetScore() => healAmount;
}
