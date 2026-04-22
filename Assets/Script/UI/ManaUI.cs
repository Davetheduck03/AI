using UnityEngine;
using UnityEngine.UI;

public class ManaUI : MonoBehaviour
{
    private ManaComponent manaComponent;
    [SerializeField] private Image fill;

    private void Start()
    {
        manaComponent = GetComponentInParent<ManaComponent>();
    }

    private void Update()
    {
        if (manaComponent != null && fill != null)
        {
            fill.fillAmount = manaComponent.currentMana / manaComponent.maxMana;
        }
    }
}
