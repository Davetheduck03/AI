using UnityEngine;

/// <summary>
/// The Relic — a unique item that triggers extraction mode when picked up.
/// GetScore() returns float.MaxValue so EvaluateNearbyItems always treats
/// it as the highest-priority item regardless of equipped gear.
/// </summary>
[CreateAssetMenu(fileName = "Relic", menuName = "Inventory/Relic")]
public class RelicSO : ItemSO
{
    public override float GetScore() => float.MaxValue;
}
