using UnityEngine;

[CreateAssetMenu(fileName = "New Head Armor", menuName = "Equipment/Head Armor")]
public class HeadArmorSO : ItemSO
{
    // statValue = flat armor value, score is just the armor itself
    public override float GetScore() => statValue;
}