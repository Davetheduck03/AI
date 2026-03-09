using UnityEngine;

public class EquipmentComponent : UnitComponent
{
	public WeaponSO equippedWeapon { get; private set; }
	public HeadArmorSO equippedHead { get; private set; }
	public BodyArmorSO equippedBody { get; private set; }

	private HealthComponent healthComp;
	private DamageComponent damageComp;
	private AdventurerClassSO adventurerClass;

	private float appliedWeaponDamage = 0f;
	private float appliedWeaponAttackSpeed = 0f;
	private float appliedHeadArmor = 0f;
	private float appliedBodyArmor = 0f;

	[Header("Loot Drop")]
	[SerializeField] private GameObject worldItemPrefab;

	protected override void OnInitialize()
	{
		healthComp = GetComponent<HealthComponent>();
		damageComp = GetComponent<DamageComponent>();

		if (data is HeroSO heroData)
		{
			adventurerClass = heroData.adventurerClass;
			if (adventurerClass == null)
				Debug.LogWarning($"[EquipmentComponent] {gameObject.name} has no AdventurerClass in HeroSO!");
			else
				Debug.Log($"[EquipmentComponent] {gameObject.name} initialized as {adventurerClass.className}");
		}
	}

	// ─────────────────────────────────────────────
	// PUBLIC ENTRY POINTS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Called by WorldItem when an adventurer walks over it.
	/// Assesses stat difference — equips and drops old if better, destroys world item if not.
	/// </summary>
	public void TryEquipWithAssessment(ItemSO newItem, Vector3 worldItemPosition)
	{
		if (newItem == null) return;

		float newScore = newItem.GetScore();
		float currentScore = GetCurrentScore(newItem);

		bool isBetter = IsBetterItem(newItem, newScore, currentScore);

		Debug.Log($"[Equipment] {gameObject.name} assessing {newItem.itemName}: " +
				  $"new={newScore:F1} current={currentScore:F1} → {(isBetter ? "EQUIP" : "SKIP")}");

		if (!isBetter)
		{
			Debug.Log($"[Equipment] {gameObject.name} ignores {newItem.itemName} — current gear is better.");
			return;
		}

		// Drop current item before equipping new one
		DropCurrentItem(newItem, worldItemPosition);

		// Equip the new item (suppress score check — we already assessed)
		ForceEquip(newItem);

		// Destroy the world item
		// WorldItem destroys itself via the trigger — handled in WorldItem.cs
	}

	/// <summary>
	/// Original path — used internally and for direct equips (e.g. starting gear).
	/// </summary>
	public bool TryEquip(ItemSO newItem)
	{
		if (newItem is WeaponSO weapon) return TryEquipWeapon(weapon);
		if (newItem is HeadArmorSO head) return TryEquipHead(head);
		if (newItem is BodyArmorSO body) return TryEquipBody(body);
		return false;
	}

	// ─────────────────────────────────────────────
	// ASSESSMENT HELPERS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Gets the score of whichever currently equipped item matches the slot of newItem.
	/// Returns 0 if slot is empty.
	/// </summary>
	private float GetCurrentScore(ItemSO newItem)
	{
		if (newItem is WeaponSO) return equippedWeapon?.GetScore() ?? 0f;
		if (newItem is HeadArmorSO) return equippedHead?.GetScore() ?? 0f;
		if (newItem is BodyArmorSO) return equippedBody?.GetScore() ?? 0f;
		return 0f;
	}

	/// <summary>
	/// Returns true if the new item is worth equipping.
	/// Handles class restriction for weapons.
	/// </summary>
	private bool IsBetterItem(ItemSO newItem, float newScore, float currentScore)
	{
		// Class restriction check for weapons
		if (newItem is WeaponSO weapon && adventurerClass != null)
		{
			if (!adventurerClass.CanEquipWeapon(weapon))
			{
				Debug.Log($"[Equipment] {gameObject.name} cannot equip {weapon.itemName} " +
						  $"(class restriction: {adventurerClass.className})");
				return false;
			}
		}

		return newScore > currentScore;
	}

	// ─────────────────────────────────────────────
	// DROP LOGIC
	// ─────────────────────────────────────────────

	/// <summary>
	/// Spawns the currently equipped item in the given slot as a WorldItem at dropPosition.
	/// Only drops if that slot matches the incoming item type.
	/// </summary>
	private void DropCurrentItem(ItemSO incomingItem, Vector3 dropPosition)
	{
		ItemSO toDrop = null;

		if (incomingItem is WeaponSO && equippedWeapon != null)
			toDrop = equippedWeapon;
		else if (incomingItem is HeadArmorSO && equippedHead != null)
			toDrop = equippedHead;
		else if (incomingItem is BodyArmorSO && equippedBody != null)
			toDrop = equippedBody;

		if (toDrop == null || worldItemPrefab == null) return;

		Vector3 scatter = new Vector3(
			Random.Range(-0.4f, 0.4f),
			Random.Range(-0.4f, 0.4f), 0);

		GameObject dropped = Instantiate(worldItemPrefab, dropPosition + scatter, Quaternion.identity);
		WorldItem worldItem = dropped.GetComponent<WorldItem>();
		if (worldItem != null)
			worldItem.item = toDrop;

		Debug.Log($"[Equipment] {gameObject.name} dropped {toDrop.itemName} at {dropPosition}");
	}

	// ─────────────────────────────────────────────
	// FORCE EQUIP (bypasses score check)
	// ─────────────────────────────────────────────

	private void ForceEquip(ItemSO newItem)
	{
		if (newItem is WeaponSO weapon) ForceEquipWeapon(weapon);
		else if (newItem is HeadArmorSO head) ForceEquipHead(head);
		else if (newItem is BodyArmorSO body) ForceEquipBody(body);
	}

	private void ForceEquipWeapon(WeaponSO newWeapon)
	{
		if (equippedWeapon != null)
		{
			damageComp?.AddDamageBonus(-appliedWeaponDamage);
			damageComp?.AddAttackSpeedBonus(-appliedWeaponAttackSpeed);
		}

		equippedWeapon = newWeapon;
		appliedWeaponDamage = newWeapon.damageBonus;
		appliedWeaponAttackSpeed = newWeapon.attackSpeedBonus;

		damageComp?.AddDamageBonus(appliedWeaponDamage);
		damageComp?.AddAttackSpeedBonus(appliedWeaponAttackSpeed);

		Debug.Log($"[Equipment] {gameObject.name} equipped {newWeapon.itemName} → " +
				  $"Total: {damageComp?.TotalDamage} dmg, {damageComp?.TotalAttackSpeed}/s");
	}

	private void ForceEquipHead(HeadArmorSO newHead)
	{
		if (equippedHead != null)
			healthComp?.AddArmorBonus(-appliedHeadArmor);

		equippedHead = newHead;
		appliedHeadArmor = newHead.statValue;
		healthComp?.AddArmorBonus(appliedHeadArmor);

		Debug.Log($"[Equipment] {gameObject.name} equipped {newHead.itemName} → " +
				  $"{healthComp?.DamageReduction:P0} total reduction");
	}

	private void ForceEquipBody(BodyArmorSO newBody)
	{
		if (equippedBody != null)
			healthComp?.AddArmorBonus(-appliedBodyArmor);

		equippedBody = newBody;
		appliedBodyArmor = newBody.statValue;
		healthComp?.AddArmorBonus(appliedBodyArmor);

		Debug.Log($"[Equipment] {gameObject.name} equipped {newBody.itemName} → " +
				  $"{healthComp?.DamageReduction:P0} total reduction");
	}

	// ─────────────────────────────────────────────
	// ORIGINAL EQUIP (used by TryEquip, kept for compatibility)
	// ─────────────────────────────────────────────

	private bool TryEquipWeapon(WeaponSO newWeapon)
	{
		if (adventurerClass != null && !adventurerClass.CanEquipWeapon(newWeapon))
			return false;

		if (equippedWeapon != null && newWeapon.GetScore() <= equippedWeapon.GetScore())
			return false;

		if (equippedWeapon != null)
		{
			damageComp?.AddDamageBonus(-appliedWeaponDamage);
			damageComp?.AddAttackSpeedBonus(-appliedWeaponAttackSpeed);
		}

		equippedWeapon = newWeapon;
		appliedWeaponDamage = newWeapon.damageBonus;
		appliedWeaponAttackSpeed = newWeapon.attackSpeedBonus;

		damageComp?.AddDamageBonus(appliedWeaponDamage);
		damageComp?.AddAttackSpeedBonus(appliedWeaponAttackSpeed);
		return true;
	}

	private bool TryEquipHead(HeadArmorSO newHead)
	{
		if (equippedHead != null && newHead.GetScore() <= equippedHead.GetScore())
			return false;

		if (equippedHead != null)
			healthComp?.AddArmorBonus(-appliedHeadArmor);

		equippedHead = newHead;
		appliedHeadArmor = newHead.statValue;
		healthComp?.AddArmorBonus(appliedHeadArmor);
		return true;
	}

	private bool TryEquipBody(BodyArmorSO newBody)
	{
		if (equippedBody != null && newBody.GetScore() <= equippedBody.GetScore())
			return false;

		if (equippedBody != null)
			healthComp?.AddArmorBonus(-appliedBodyArmor);

		equippedBody = newBody;
		appliedBodyArmor = newBody.statValue;
		healthComp?.AddArmorBonus(appliedBodyArmor);
		return true;
	}

	public void LogLoadout()
	{
		Debug.Log($"[{gameObject.name} Loadout] " +
				  $"Weapon: {(equippedWeapon != null ? equippedWeapon.itemName : "none")} | " +
				  $"Head: {(equippedHead != null ? equippedHead.itemName : "none")} | " +
				  $"Body: {(equippedBody != null ? equippedBody.itemName : "none")}");
	}
}