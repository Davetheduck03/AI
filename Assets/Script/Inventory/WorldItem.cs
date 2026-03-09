using UnityEngine;

public class WorldItem : MonoBehaviour
{
	public ItemSO item;

	private void OnTriggerEnter2D(Collider2D other)
	{
		EquipmentComponent eq = other.GetComponent<EquipmentComponent>();
		if (eq == null) return;

		// Pass our position so dropped gear lands here
		float currentScore = GetScoreOnUnit(eq);
		float newScore = item.GetScore();

		// Let EquipmentComponent decide — if it equips, we destroy ourselves
		bool willEquip = newScore > currentScore && CanEquip(eq);
		if (!willEquip) return;

		eq.TryEquipWithAssessment(item, transform.position);
		Destroy(gameObject);
	}

	private float GetScoreOnUnit(EquipmentComponent eq)
	{
		if (item is WeaponSO) return eq.equippedWeapon?.GetScore() ?? 0f;
		if (item is HeadArmorSO) return eq.equippedHead?.GetScore() ?? 0f;
		if (item is BodyArmorSO) return eq.equippedBody?.GetScore() ?? 0f;
		return 0f;
	}

	private bool CanEquip(EquipmentComponent eq)
	{
		// Weapon class restriction is handled inside EquipmentComponent
		// This just avoids calling TryEquipWithAssessment for non-weapon items unnecessarily
		return true;
	}
}