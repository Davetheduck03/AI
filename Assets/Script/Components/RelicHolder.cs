using System;
using UnityEngine;

/// <summary>
/// Attached to the hero. Tracks whether the hero is carrying the Relic.
/// When HasRelic becomes true the hero's BT extraction sequence becomes active,
/// overriding the normal combat/loot/explore priorities.
/// </summary>
public class RelicHolder : MonoBehaviour
{
    /// <summary>Fired when the hero picks up the relic. Carries the holder component.</summary>
    public static event Action<RelicHolder> OnRelicPickedUp;

    public bool HasRelic { get; private set; } = false;

    private EquipmentComponent equipmentComp;

    private void Awake()
    {
        equipmentComp = GetComponent<EquipmentComponent>();
    }

    /// <summary>
    /// Call this when the hero picks up a RelicSO world item.
    /// Marks possession, registers it in the fourth equipment slot, and fires the pickup event.
    /// </summary>
    public void PickupRelic(RelicSO relic)
    {
        HasRelic = true;
        equipmentComp?.ForceEquipRelic(relic);

        // Broadcast to the team board so every hero's BT can see who carries the relic
        TeamBlackboard.Instance?.SetRelicHolder(transform);

        Debug.Log($"[RelicHolder] {gameObject.name} picked up the Relic: {relic?.itemName}");
        OnRelicPickedUp?.Invoke(this);
    }

    /// <summary>
    /// Resets possession — called by RoundState_Win before regenerating the next floor.
    /// Also clears the relic from the fourth equipment slot so the UI updates immediately.
    /// </summary>
    public void Reset()
    {
        HasRelic = false;
        equipmentComp?.ClearRelic();
        TeamBlackboard.Instance?.ClearRelicHolder();
    }
}
