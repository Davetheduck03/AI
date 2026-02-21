using UnityEngine;

[CreateAssetMenu(fileName = "WeaponSO", menuName = "Equipment/WeaponSO")]
public class WeaponSO : ItemSO
{
    public float attackDamageValue;
    public float attackSpeedValue;

    public override float GetScore()
    {
        throw new System.NotImplementedException();
    }
}
