using UnityEngine;

public class EquipmentComponent : UnitComponent
{
	public WeaponSO equippedWeapon { get; private set; }
	public HeadArmorSO equippedHead { get; private set; }
	public BodyArmorSO equippedBody { get; private set; }
	public RelicSO equippedRelic { get; private set; }

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
				Debug.LogWarning($"[EquipmentComponent] {gameObject.name} has no AdventurerClass in HeroSO! " +
				                 "This hero will be able to equip any weapon type.");
			else
				Debug.Log($"[EquipmentComponent] {gameObject.name} initialized as {adventurerClass.className}");
		}
	}

	/// <summary>
	/// Equips the class's starting weapon after all Awake/OnInitialize calls have
	/// completed, so DamageComponent is fully set up before SetWeaponRange is called.
	/// </summary>
	private void Start()
	{
		if (adventurerClass?.startingWeapon != null)
		{
			ForceEquipWeapon(adventurerClass.startingWeapon);
			Debug.Log($"[Equipment] {gameObject.name} starts with {adventurerClass.startingWeapon.itemName}");
		}
	}

	// ─────────────────────────────────────────────
	// PUBLIC ENTRY POINTS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Called by WorldItem / PickupItem when an adventurer picks up an item.
	/// Compares scores — equips and drops old if better.
	/// Returns true if the item was equipped, false if it was rejected.
	/// </summary>
	public bool TryEquipWithAssessment(ItemSO newItem, Vector3 worldItemPosition)
	{
		if (newItem == null) return false;

		float newScore     = newItem.GetScore();
		float currentScore = GetCurrentScore(newItem);

		bool isBetter = IsBetterItem(newItem, newScore, currentScore);

		Debug.Log($"[Equipment] {gameObject.name} assessing {newItem.itemName}: " +
				  $"new={newScore:F1} current={currentScore:F1} → {(isBetter ? "EQUIP" : "SKIP")}");

		if (!isBetter)
		{
			Debug.Log($"[Equipment] {gameObject.name} ignores {newItem.itemName} — current gear is better.");
			return false;
		}

		// Drop current item before equipping new one
		DropCurrentItem(newItem);

		// Equip the new item (score already assessed above)
		ForceEquip(newItem);
		return true;
	}

	/// <summary>
	/// Direct equip path — used for starting gear and other programmatic equips.
	/// Checks class restrictions and score, but does NOT drop the old item.
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
	/// Returns the score of whichever currently equipped item occupies the same slot as newItem.
	/// Returns 0 if the slot is empty.
	/// </summary>
	private float GetCurrentScore(ItemSO newItem)
	{
		if (newItem is WeaponSO)    return equippedWeapon?.GetScore() ?? 0f;
		if (newItem is HeadArmorSO) return equippedHead?.GetScore()   ?? 0f;
		if (newItem is BodyArmorSO) return equippedBody?.GetScore()   ?? 0f;
		return 0f;
	}

	/// <summary>
	/// Returns true if the new item is worth equipping.
	/// Note: a null adventurerClass means no weapon-type restrictions apply for this hero.
	/// </summary>
	private bool IsBetterItem(ItemSO newItem, float newScore, float currentScore)
	{
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
	/// Spawns the currently equipped item (for the same slot as incomingItem) as a WorldItem
	/// at the hero's feet. Does nothing if the slot is empty or worldItemPrefab is not assigned.
	/// </summary>
	private void DropCurrentItem(ItemSO incomingItem)
	{
		ItemSO toDrop = null;

		if      (incomingItem is WeaponSO    && equippedWeapon != null) toDrop = equippedWeapon;
		else if (incomingItem is HeadArmorSO && equippedHead   != null) toDrop = equippedHead;
		else if (incomingItem is BodyArmorSO && equippedBody   != null) toDrop = equippedBody;

		if (toDrop == null) return;

		if (worldItemPrefab == null)
		{
			Debug.LogWarning($"[Equipment] {gameObject.name} tried to drop {toDrop.itemName} " +
			                 "but worldItemPrefab is not assigned on EquipmentComponent!");
			return;
		}

		// Drop at the hero's current position with a small offset so it doesn't
		// land exactly under them (which would immediately re-trigger pickup).
		Vector3 scatter = new Vector3(Random.Range(0.5f, 0.8f), 0, 0);
		scatter = Quaternion.Euler(0, 0, Random.Range(0f, 360f)) * scatter;
		Vector3 spawnPos = transform.position + scatter;

		GameObject dropped = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
		WorldItem worldItem = dropped.GetComponent<WorldItem>();
		if (worldItem != null)
			worldItem.item = toDrop;

		Debug.Log($"[Equipment] {gameObject.name} dropped {toDrop.itemName} at {spawnPos}");
	}

	// ─────────────────────────────────────────────
	// FORCE EQUIP — bypasses score check, used internally
	// ─────────────────────────────────────────────

	private void ForceEquip(ItemSO newItem)
	{
		if      (newItem is WeaponSO weapon)    ForceEquipWeapon(weapon);
		else if (newItem is HeadArmorSO head)   ForceEquipHead(head);
		else if (newItem is BodyArmorSO body)   ForceEquipBody(body);
	}

	private void ForceEquipWeapon(WeaponSO newWeapon)
	{
		if (equippedWeapon != null)
		{
			damageComp?.AddDamageBonus(-appliedWeaponDamage);
			damageComp?.AddAttackSpeedBonus(-appliedWeaponAttackSpeed);
		}

		equippedWeapon           = newWeapon;
		appliedWeaponDamage      = newWeapon.damageBonus;
		appliedWeaponAttackSpeed = newWeapon.attackSpeedBonus;

		damageComp?.AddDamageBonus(appliedWeaponDamage);
		damageComp?.AddAttackSpeedBonus(appliedWeaponAttackSpeed);
		damageComp?.SetWeaponRange(newWeapon.range);   // 0 = hero base range, >0 = weapon override

		Debug.Log($"[Equipment] {gameObject.name} equipped {newWeapon.itemName} → " +
				  $"Total: {damageComp?.TotalDamage} dmg, {damageComp?.TotalAttackSpeed}/s, " +
				  $"Range: {damageComp?.AttackRange}");
	}

	private void ForceEquipHead(HeadArmorSO newHead)
	{
		if (equippedHead != null)
			healthComp?.AddArmorBonus(-appliedHeadArmor);

		equippedHead      = newHead;
		appliedHeadArmor  = newHead.statValue;
		healthComp?.AddArmorBonus(appliedHeadArmor);

		Debug.Log($"[Equipment] {gameObject.name} equipped {newHead.itemName} → " +
				  $"{healthComp?.DamageReduction:P0} total reduction");
	}

	private void ForceEquipBody(BodyArmorSO newBody)
	{
		if (equippedBody != null)
			healthComp?.AddArmorBonus(-appliedBodyArmor);

		equippedBody      = newBody;
		appliedBodyArmor  = newBody.statValue;
		healthComp?.AddArmorBonus(appliedBodyArmor);

		Debug.Log($"[Equipment] {gameObject.name} equipped {newBody.itemName} → " +
				  $"{healthComp?.DamageReduction:P0} total reduction");
	}

	// ─────────────────────────────────────────────
	// GATED EQUIP — checks restrictions & score, then delegates to ForceEquip*
	// ─────────────────────────────────────────────

	private bool TryEquipWeapon(WeaponSO newWeapon)
	{
		if (adventurerClass != null && !adventurerClass.CanEquipWeapon(newWeapon))
			return false;
		if (equippedWeapon != null && newWeapon.GetScore() <= equippedWeapon.GetScore())
			return false;
		ForceEquipWeapon(newWeapon);
		return true;
	}

	private bool TryEquipHead(HeadArmorSO newHead)
	{
		if (equippedHead != null && newHead.GetScore() <= equippedHead.GetScore())
			return false;
		ForceEquipHead(newHead);
		return true;
	}

	private bool TryEquipBody(BodyArmorSO newBody)
	{
		if (equippedBody != null && newBody.GetScore() <= equippedBody.GetScore())
			return false;
		ForceEquipBody(newBody);
		return true;
	}

	// ─────────────────────────────────────────────
	// RELIC SLOT
	// ─────────────────────────────────────────────

	/// <summary>
	/// Called by RelicHolder when the hero picks up the relic world item.
	/// Stores the reference so it can be dropped on death.
	/// </summary>
	public void ForceEquipRelic(RelicSO relic)
	{
		equippedRelic = relic;
		Debug.Log($"[Equipment] {gameObject.name} is now carrying the relic: {relic?.itemName}");
	}

	/// <summary>
	/// Clears the relic slot without dropping it — called at the end of a successful floor
	/// so the hero starts the next floor without the relic in their inventory or UI.
	/// </summary>
	public void ClearRelic()
	{
		equippedRelic = null;
		Debug.Log($"[Equipment] {gameObject.name} relic slot cleared.");
	}

	/// <summary>
	/// Called by HealthComponent just before the hero is destroyed.
	/// Drops the relic as a world item so another hero can pick it up.
	/// </summary>
	public void DropOnDeath()
	{
		if (equippedRelic == null) return;

		if (worldItemPrefab == null)
		{
			Debug.LogWarning($"[Equipment] {gameObject.name} died with the relic but " +
			                 "worldItemPrefab is not assigned — relic lost!");
			return;
		}

		Vector3 scatter = new Vector3(Random.Range(0.3f, 0.6f), 0, 0);
		scatter = Quaternion.Euler(0, 0, Random.Range(0f, 360f)) * scatter;
		Vector3 spawnPos = transform.position + scatter;

		GameObject dropped = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
		WorldItem worldItem = dropped.GetComponent<WorldItem>();
		if (worldItem != null)
			worldItem.item = equippedRelic;

		Debug.Log($"[Equipment] {gameObject.name} died — dropped relic '{equippedRelic.itemName}' at {spawnPos}");
		equippedRelic = null;
	}

	public void LogLoadout()
	{
		Debug.Log($"[{gameObject.name} Loadout] " +
				  $"Weapon: {(equippedWeapon != null ? equippedWeapon.itemName : "none")} | " +
				  $"Head: {(equippedHead   != null ? equippedHead.itemName   : "none")} | " +
				  $"Body: {(equippedBody   != null ? equippedBody.itemName   : "none")} | " +
				  $"Relic: {(equippedRelic  != null ? equippedRelic.itemName  : "none")}");
	}
}
