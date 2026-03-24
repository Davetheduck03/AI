using UnityEngine;
using UnityEngine.UI;

public class HealthUI : MonoBehaviour
{
    private HealthComponent healthComponent;
    [SerializeField] private Image fill;

    private void Start()
    {
        healthComponent = GetComponentInParent<HealthComponent>();
    }

    private void Update()
    {
        if (healthComponent != null && fill != null)
        {
            fill.fillAmount = healthComponent.currentHealth / healthComponent.maxHealth;
        }
    }
}
