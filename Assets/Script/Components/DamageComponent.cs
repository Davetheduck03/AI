using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageComponent : UnitComponent
{
    private float damage;


    protected override void OnInitialize()
    {
        damage = data.damage;
    }

    public void TryDealDamage(GameObject target)
    {
        if (target.TryGetComponent<HealthComponent>(out HealthComponent health))
        {
            if (health.isDamagable)
            {
                DamageData damageData = new DamageData(damage, this.gameObject);
                health.TakeDamage(damageData);
            }
            else return;
        }
    }
}
