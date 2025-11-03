using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct DamageData
{
    public float amount;
    public GameObject damageDealer;

    public DamageData(float amount, GameObject damageDealer)
    {
        this.amount = amount;
        this.damageDealer = damageDealer;
    }

}
