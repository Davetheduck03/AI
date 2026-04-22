using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CONDITION/ACTION: Searches WorldItemRegistry for the nearest uncollected potion
/// that this hero still has room for, within <paramref name="searchRange"/> units.
///
/// Writes the potion WorldItem's Transform to bb[<paramref name="targetKey"/>].
/// The hero then walks to it via MoveTowardsTarget; WorldItem.OnTriggerEnter2D
/// handles the actual pickup automatically on contact.
///
/// Skips potions whose type the hero's slots are already full for, so heroes
/// never make a detour for something they cannot carry.
///
/// Returns Success when a reachable, wanted potion is found.
/// Returns Failure when all nearby potions are either out of range or unneeded.
/// </summary>
public class FindPotionInRange : Node
{
    private readonly float  _searchRange;
    private readonly string _targetKey;

    public FindPotionInRange(Blackboard bb,
                             float  searchRange = 12f,
                             string targetKey   = "itemTarget") : base(bb)
    {
        _searchRange = searchRange;
        _targetKey   = targetKey;
    }

    public override NodeState Evaluate()
    {
        var self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var equip = self.GetComponent<EquipmentComponent>();
        if (equip == null) return NodeState.Failure;

        Transform bestTarget = null;
        float     bestDist   = float.MaxValue;

        // Snapshot the registry so destroyed items mid-iteration don't cause errors
        foreach (var worldItem in new List<WorldItem>(WorldItemRegistry.All))
        {
            if (worldItem == null || worldItem.item == null) continue;

            bool wantIt = false;

            if (worldItem.item is HealthPotionSO hp)
            {
                // Has room in slot 1 or slot 2?
                bool slot1Room = equip.equippedHealthPotion  == null
                              || equip.healthPotionCount     <  equip.equippedHealthPotion.maxStack;
                bool slot2Room = equip.equippedHealthPotion2 == null
                              || equip.healthPotionCount2    <  equip.equippedHealthPotion2.maxStack;
                wantIt = slot1Room || slot2Room;
            }
            else if (worldItem.item is ManaPotionSO mp)
            {
                bool slot1Room = equip.equippedManaPotion  == null
                              || equip.manaPotionCount     <  equip.equippedManaPotion.maxStack;
                bool slot2Room = equip.equippedManaPotion2 == null
                              || equip.manaPotionCount2    <  equip.equippedManaPotion2.maxStack;
                wantIt = slot1Room || slot2Room;
            }

            if (!wantIt) continue;

            float dist = Vector3.Distance(self.position, worldItem.transform.position);
            if (dist > _searchRange || dist >= bestDist) continue;

            bestDist   = dist;
            bestTarget = worldItem.transform;
        }

        if (bestTarget == null)
        {
            bb.Set<Transform>(_targetKey, null);
            return NodeState.Failure;
        }

        bb.Set(_targetKey, bestTarget);
        return NodeState.Success;
    }
}
