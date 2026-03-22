using UnityEngine;

/// <summary>
/// Base class for all hero units.
/// Listens to HealthComponent.OnDeath and triggers the Lose state when
/// the hero dies so the dungeon can regenerate.
/// </summary>
public class BaseHero : BaseUnit
{
    private void OnEnable()
    {
        HealthComponent.OnDeath += HandleDeath;
        CameraController.RegisterHero(this);
    }

    private void OnDisable()
    {
        HealthComponent.OnDeath -= HandleDeath;
        CameraController.UnregisterHero(this);
    }

    private void HandleDeath(HealthComponent who)
    {
        // Only react to our own death
        if (who.gameObject != gameObject) return;

        if (GameManager.Instance != null)
            GameManager.Instance.SwitchState(GameManager.Instance.Lose);
    }
}
