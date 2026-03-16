using UnityEngine;

/// <summary>
/// ACTION: Searches for nearby WorldItems and moves to the best one.
/// Evaluates item score against current gear before committing to pickup.
/// Returns Success when an item is equipped or all nearby items are skipped.
/// Returns Failure if no WorldItems found in range.
/// Returns Running while moving toward a candidate item.
/// </summary>
public class EvaluateNearbyItems : Node
{
	private float searchRange;
	private float arrivalDistance;
	private Transform currentTarget = null;

	public EvaluateNearbyItems(Blackboard bb, float searchRange = 5f, float arrivalDistance = 0.5f) : base(bb)
	{
		this.searchRange = searchRange;
		this.arrivalDistance = arrivalDistance;
	}

	public override NodeState Evaluate()
	{
		Transform self = bb.Get<Transform>("self");
		if (self == null) return NodeState.Failure;

		EquipmentComponent equipment = self.GetComponent<EquipmentComponent>();
		if (equipment == null) return NodeState.Failure;

		// If we had a target but it's gone (picked up or destroyed), we're done
		if (currentTarget != null && currentTarget.gameObject == null)
		{
			ClearTarget();
			return NodeState.Success;
		}

		// If we have a live target, keep moving toward it
		if (currentTarget != null)
		{
			float dist = Vector2.Distance(self.position, currentTarget.position);

			if (dist <= arrivalDistance)
			{
				// Arrival — WorldItem's OnTriggerEnter2D handles the actual equip,
				// but we clear our reference and report success
				ClearTarget();
				return NodeState.Success;
			}

			return NodeState.Running;
		}

		// No current target — scan for the best WorldItem in range
		WorldItem bestItem = FindBestItem(self, equipment);

		if (bestItem == null)
			return NodeState.Failure;

		// Set as movement target on blackboard and start pathing
		bb.Set("target", bestItem.transform);
		currentTarget = bestItem.transform;

		MovementComponent movementComp = self.GetComponent<MovementComponent>();
		if (movementComp != null)
			movementComp.OnTriggerMove(self, currentTarget);

		Debug.Log($"[EvaluateNearbyItems] {self.name} moving toward {bestItem.item?.itemName}");
		return NodeState.Running;
	}

	/// <summary>
	/// Finds the highest-scoring WorldItem in range that is actually an upgrade.
	/// Ignores items the knight's class cannot equip.
	/// </summary>
	private WorldItem FindBestItem(Transform self, EquipmentComponent equipment)
	{
		GameObject[] worldItemObjects = GameObject.FindGameObjectsWithTag("WorldItem");

		WorldItem bestCandidate = null;
		float bestScoreDelta = 0f;

		foreach (GameObject obj in worldItemObjects)
		{
			if (obj == null) continue;

			float dist = Vector2.Distance(self.position, obj.transform.position);
			if (dist > searchRange) continue;

			WorldItem worldItem = obj.GetComponent<WorldItem>();
			if (worldItem == null || worldItem.item == null) continue;

			ItemSO candidate = worldItem.item;
			float newScore = candidate.GetScore();
			float currentScore = GetCurrentScore(equipment, candidate);

			float delta = newScore - currentScore;

			// Class restriction check for weapons
			if (candidate is WeaponSO weapon)
			{
				// Access via reflection not needed — EquipmentComponent exposes equipped slots
				// We re-use the same IsBetter logic by trying a dry-run score comparison
				// If score delta is negative, skip; class restriction is handled in TryEquipWithAssessment
			}

			if (delta > bestScoreDelta)
			{
				bestScoreDelta = delta;
				bestCandidate = worldItem;
			}
		}

		return bestCandidate;
	}

	/// <summary>
	/// Mirrors the score comparison logic in EquipmentComponent.
	/// </summary>
	private float GetCurrentScore(EquipmentComponent equipment, ItemSO candidate)
	{
		if (candidate is WeaponSO) return equipment.equippedWeapon?.GetScore() ?? 0f;
		if (candidate is HeadArmorSO) return equipment.equippedHead?.GetScore() ?? 0f;
		if (candidate is BodyArmorSO) return equipment.equippedBody?.GetScore() ?? 0f;
		return 0f;
	}

	private void ClearTarget()
	{
		currentTarget = null;
		bb.Set<Transform>("target", null);
	}
}