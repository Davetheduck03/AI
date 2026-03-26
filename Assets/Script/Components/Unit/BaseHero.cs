using UnityEngine;

/// <summary>
/// Base class for all hero units.
/// Listens to HealthComponent.OnDeath and triggers the Lose state when
/// the hero dies so the dungeon can regenerate.
/// </summary>
public class BaseHero : BaseUnit
{
    /// <summary>
    /// 0-based index assigned by DungeonSpawner at spawn time (0 = Player 1, etc.).
    /// Used by UnitNameUI and PartyEquipmentPanelUI.
    /// </summary>
    [HideInInspector] public int playerIndex = 0;

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

        // Notify the spawner — it tracks the living count and only triggers
        // Lose when the last hero dies, allowing the others to keep fighting.
        DungeonSpawner.Instance?.OnHeroDied(this);
    }
}
