using UnityEngine;

[CreateAssetMenu(fileName = "New Body Armor", menuName = "Equipment/Body Armor")]
public class BodyArmorSO : ItemSO
{
    // statValue = flat armor value, score is just the armor itself
    public override float GetScore() => statValue;
}