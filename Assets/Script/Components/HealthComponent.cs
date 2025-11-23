using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class HealthComponent : UnitComponent
{
    private float currentHealth;
    public float currentArmour;
    public bool isDamagable;


    protected override void OnInitialize()
    {
        currentHealth = data.Health;
        isDamagable = true;
        currentArmour = data.armor;
    }

    public void TakeDamage(DamageData data)
    {
        if (!isDamagable) return;
        float baseAmount = data.amount;

        if (currentArmour > 0)
        {
            currentArmour -= baseAmount * 0.5f;
            if (currentArmour < 0)
            {
                float overflowDamage = -currentArmour * 2f;
                currentHealth -= overflowDamage;
                currentArmour = 0;
            }
        }

        else if(currentArmour <= 0)
        {
            currentHealth -= baseAmount;
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}
