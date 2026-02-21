using UnityEngine;

public abstract class UnitSO : ScriptableObject
{
    [Header("Basic Info")]
    public string UnitName;
    public GameObject UnitPrefab;
    public Sprite Icon;

    [Header("Base Stats")]
    public float Health;
    public float Speed;
    public float goldReward;

    [Header("Combat Stats")]
    public float baseDamage;
    public float baseAttackSpeed;   // Attacks per second, e.g. 1.0 = 1 attack/sec
    public float armor;
}