using UnityEngine;

/// <summary>
/// Abstract base for all stackable consumable potions.
/// The item slot in EquipmentComponent tracks both the SO (type) and the current stack count.
/// maxStack is set per-SO in the Inspector.
/// </summary>
public abstract class PotionSO : ItemSO
{
    [Header("Stack")]
    [Tooltip("Maximum number of this potion that fit in one inventory slot.")]
    public int maxStack = 5;
}
