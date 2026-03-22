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

    /// <summary>
    /// Call this when the hero picks up a RelicSO world item.
    /// Marks possession and fires the pickup event.
    /// </summary>
    public void PickupRelic(RelicSO relic)
    {
        HasRelic = true;
        Debug.Log($"[RelicHolder] {gameObject.name} picked up the Relic: {relic?.itemName}");
        OnRelicPickedUp?.Invoke(this);
    }

    /// <summary>
    /// Resets possession — called by RoundState_Win before regenerating the next floor.
    /// </summary>
    public void Reset()
    {
        HasRelic = false;
    }
}
