using UnityEngine;

[CreateAssetMenu(fileName = "New Hero Data", menuName = "AI/Units/Hero")]
public class HeroSO : UnitSO
{
    [Header("Class")]
    [Tooltip("Determines which weapons and abilities this hero can use.")]
    public AdventurerClassSO adventurerClass;

    [Header("Hero Stats")]
    public float fireRate;
    public float range;
    public bool isAoE;
    public float projectileSpeed;
}