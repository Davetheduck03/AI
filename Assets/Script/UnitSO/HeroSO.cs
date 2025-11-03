using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Hero Data", menuName = "AI/Units/Hero")]
public class HeroSO : UnitSO
{
    public enum HeroType
    {
        Knight,
        Paladin,
        Support,
        Mage
    }

    [Header("Hero Stats")]
    public float fireRate;
    public float range;
    public bool isAoE;
    public float projectileSpeed;
    public HeroType heroType;
}
