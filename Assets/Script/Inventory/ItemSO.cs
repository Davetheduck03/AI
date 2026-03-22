using UnityEngine;

[CreateAssetMenu(fileName = "ItemSO", menuName = "Inventory/ItemSO")]
public abstract class ItemSO : ScriptableObject
{
    [Header("Info")]
    public int ID;
    public string itemName;
    public string Description;
    public Sprite Icon;

    /// <summary>Returns a comparable score used by agents to decide whether to equip this item.</summary>
    public abstract float GetScore();
}
