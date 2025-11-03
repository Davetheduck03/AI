using System.Collections;
using System.Collections.Generic;
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
    public float damage;


    [Header("Optional Stats")]
    public float armor;
}
