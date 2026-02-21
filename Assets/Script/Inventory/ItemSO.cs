using UnityEngine;

[CreateAssetMenu(fileName = "ItemSO", menuName = "Inventory/ItemSO")]
public abstract class ItemSO : ScriptableObject
{
    [Header("Info")]
    public int ID;
    public string itemName;
    public string Description;
    public Sprite Icon;

    [Header("Value")]
    public float statValue;

    public abstract float GetScore();
    //Get the value for the Agents to compare equipments
}
