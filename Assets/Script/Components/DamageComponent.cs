using UnityEngine;

public class DamageComponent : UnitComponent
{
    private float baseDamage;
    private float damageBonus = 0f;

    public float TotalDamage => baseDamage + damageBonus;

    protected override void OnInitialize()
    {
        baseDamage = data.damage;
    }

    public void AddDamageBonus(float amount)
    {
        damageBonus += amount;
        Debug.Log($"[DamageComponent] {gameObject.name} damage: {baseDamage} + {damageBonus} bonus");
    }

    public void TryDealDamage(GameObject target)
    {
        if (target.TryGetComponent<HealthComponent>(out HealthComponent health))
        {
            if (health.isDamagable)
            {
                DamageData damageData = new DamageData(TotalDamage, this.gameObject);
                health.TakeDamage(damageData);
            }
        }
    }
}