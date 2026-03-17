using UnityEngine;

/// <summary>
/// CONDITION: Scans all nearby WorldItems that are upgrades, then selects the one
/// with the highest absolute score and writes it to the blackboard as "target" and
/// "targetWorldItem" for PickupItem to consume.
/// Returns Success if a worthy item is found; Failure otherwise.
/// </summary>
public class EvaluateNearbyItems : Node
{
	private float searchRange;

	public EvaluateNearbyItems(Blackboard bb, float searchRange = 5f) : base(bb)
	{
		this.searchRange = searchRange;
	}

	public override NodeState Evaluate()
	{
		Transform self = bb.Get<Transform>("self");
		if (self == null) return NodeState.Failure;

		EquipmentComponent equipment = self.GetComponent<EquipmentComponent>();
		if (equipment == null) return NodeState.Failure;

		WorldItem bestItem = FindBestItem(self, equipment);

		if (bestItem == null)
			return NodeState.Failure;

		// Set movement target and also store the WorldItem reference for PickupItem
		bb.Set("target", bestItem.transform);
		bb.Set("targetWorldItem", bestItem);
		Debug.Log($"[EvaluateNearbyItems] {self.name} targeting {bestItem.item?.itemName} " +
		          $"(score: {bestItem.item?.GetScore():F1})");
		return NodeState.Success;
	}

	/// <summary>
	/// Among all in-range WorldItems that are genuine upgrades, returns the one
	/// with the highest absolute score. Class restrictions are pre-checked here
	/// so the hero never wastes time walking to an item it cannot equip.
	/// </summary>
	private WorldItem FindBestItem(Transform self, EquipmentComponent equipment)
	{
		GameObject[] worldItemObjects = GameObject.FindGameObjectsWithTag("WorldItem");

		WorldItem bestCandidate = null;
		float bestScore = float.MinValue;

		foreach (GameObject obj in worldItemObjects)
		{
			if (obj == null) continue;

			float dist = Vector2.Distance(self.position, obj.transform.position);
			if (dist > searchRange) continue;

			WorldItem worldItem = obj.GetComponent<WorldItem>();
			if (worldItem == null || worldItem.item == null) continue;

			ItemSO candidate = worldItem.item;

			// Skip weapons this hero class cannot equip
			if (candidate is WeaponSO weapon)
			{
				AdventurerClassSO adventurerClass = GetAdventurerClass(self);
				if (adventurerClass != null && !adventurerClass.CanEquipWeapon(weapon))
					continue;
			}

			// Only consider genuine upgrades — avoids walking to worse gear
			float newScore     = candidate.GetScore();
			float currentScore = GetCurrentScore(equipment, candidate);
			if (newScore <= currentScore) continue;

			// Among upgrades, pick the one with the highest raw score
			if (newScore > bestScore)
			{
				bestScore     = newScore;
				bestCandidate = worldItem;
			}
		}

		return bestCandidate;
	}

	private float GetCurrentScore(EquipmentComponent equipment, ItemSO candidate)
	{
		if (candidate is WeaponSO)    return equipment.equippedWeapon?.GetScore() ?? 0f;
		if (candidate is HeadArmorSO) return equipment.equippedHead?.GetScore()   ?? 0f;
		if (candidate is BodyArmorSO) return equipment.equippedBody?.GetScore()   ?? 0f;
		return 0f;
	}

	private AdventurerClassSO GetAdventurerClass(Transform self)
	{
		BaseUnit unit = self.GetComponent<BaseUnit>();
		if (unit == null) return null;
		HeroSO heroData = unit.unitData as HeroSO;
		return heroData?.adventurerClass;
	}
}
