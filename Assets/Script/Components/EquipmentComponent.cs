using UnityEngine;

public class EquipmentComponent : UnitComponent
{
	public WeaponSO     equippedWeapon { get; private set; }
	public HeadArmorSO  equippedHead   { get; private set; }
	public BodyArmorSO  equippedBody   { get; private set; }
	public RelicSO      equippedRelic  { get; private set; }

	// ── Potion slots ──────────────────────────────────────────────────────
	// Each potion type has TWO dedicated slots, each stackable to the SO's maxStack.
	// Pickup fills slot 1 first; when slot 1 is full it overflows into slot 2.
	// Consuming drains slot 1; when slot 1 empties, slot 2 is promoted into slot 1
	// so the hero always has a continuous supply without manual slot management.

	public HealthPotionSO equippedHealthPotion  { get; private set; }
	public int            healthPotionCount     { get; private set; }
	public HealthPotionSO equippedHealthPotion2 { get; private set; }
	public int            healthPotionCount2    { get; private set; }

	public ManaPotionSO   equippedManaPotion    { get; private set; }
	public int            manaPotionCount       { get; private set; }
	public ManaPotionSO   equippedManaPotion2   { get; private set; }
	public int            manaPotionCount2      { get; private set; }

	// Auto-use thresholds and cooldown
	private const float AutoHealthPotionThreshold = 0.30f;  // consume when HP < 30 %
	private const float AutoManaPotionThreshold   = 0.20f;  // consume when mana < 20 %
	private const float PotionUseCooldown         = 5f;     // seconds between auto-uses
	private float _nextPotionUseTime = 0f;

	private HealthComponent    healthComp;
	private DamageComponent    damageComp;
	private AdventurerClassSO  adventurerClass;

	private float appliedWeaponDamage      = 0f;
	private float appliedWeaponAttackSpeed = 0f;
	private float appliedHeadArmor         = 0f;
	private float appliedBodyArmor         = 0f;

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

	// ── Auto-consume potions ──────────────────────────────────────────────

	private void Update()
	{
		if (Time.time < _nextPotionUseTime) return;

		// Auto health potion — triggered at critically low HP
		if (equippedHealthPotion != null && healthPotionCount > 0 && healthComp != null)
		{
			float hpFraction = healthComp.currentHealth / healthComp.maxHealth;
			if (hpFraction < AutoHealthPotionThreshold)
			{
				ConsumeHealthPotion();
				_nextPotionUseTime = Time.time + PotionUseCooldown;
				return;
			}
		}

		// Auto mana potion — triggered at critically low mana
		if (equippedManaPotion != null && manaPotionCount > 0)
		{
			var mana = GetComponent<ManaComponent>();
			if (mana != null && mana.ManaFraction < AutoManaPotionThreshold)
			{
				ConsumeManaPotion();
				_nextPotionUseTime = Time.time + PotionUseCooldown;
			}
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

		// Potions are handled by contact pickup, not BT assessment
		if (newItem is PotionSO) return false;

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
	// POTION SLOT MANAGEMENT
	// ─────────────────────────────────────────────

	/// <summary>
	/// Adds a health potion, filling slot 1 first and overflowing to slot 2.
	/// Returns true if the potion was accepted by either slot; false if both are full.
	/// </summary>
	public bool TryAddHealthPotion(HealthPotionSO potion)
	{
		if (potion == null) return false;

		// ── Slot 1 ────────────────────────────────────────────────────────────
		if (equippedHealthPotion == null)
		{
			equippedHealthPotion = potion;
			healthPotionCount    = 1;
			Debug.Log($"[Equipment] {gameObject.name} picked up {potion.itemName} (slot 1) — 1/{potion.maxStack}");
			return true;
		}
		if (healthPotionCount < equippedHealthPotion.maxStack)
		{
			healthPotionCount++;
			Debug.Log($"[Equipment] {gameObject.name} stacked {potion.itemName} (slot 1) — {healthPotionCount}/{equippedHealthPotion.maxStack}");
			return true;
		}

		// ── Slot 2 (overflow) ─────────────────────────────────────────────────
		if (equippedHealthPotion2 == null)
		{
			equippedHealthPotion2 = potion;
			healthPotionCount2    = 1;
			Debug.Log($"[Equipment] {gameObject.name} picked up {potion.itemName} (slot 2) — 1/{potion.maxStack}");
			return true;
		}
		if (healthPotionCount2 < equippedHealthPotion2.maxStack)
		{
			healthPotionCount2++;
			Debug.Log($"[Equipment] {gameObject.name} stacked {potion.itemName} (slot 2) — {healthPotionCount2}/{equippedHealthPotion2.maxStack}");
			return true;
		}

		Debug.Log($"[Equipment] {gameObject.name} both health potion slots full — rejected {potion.itemName}");
		return false;
	}

	/// <summary>
	/// Adds a mana potion, filling slot 1 first and overflowing to slot 2.
	/// Returns true if the potion was accepted by either slot; false if both are full.
	/// </summary>
	public bool TryAddManaPotion(ManaPotionSO potion)
	{
		if (potion == null) return false;

		// ── Slot 1 ────────────────────────────────────────────────────────────
		if (equippedManaPotion == null)
		{
			equippedManaPotion = potion;
			manaPotionCount    = 1;
			Debug.Log($"[Equipment] {gameObject.name} picked up {potion.itemName} (slot 1) — 1/{potion.maxStack}");
			return true;
		}
		if (manaPotionCount < equippedManaPotion.maxStack)
		{
			manaPotionCount++;
			Debug.Log($"[Equipment] {gameObject.name} stacked {potion.itemName} (slot 1) — {manaPotionCount}/{equippedManaPotion.maxStack}");
			return true;
		}

		// ── Slot 2 (overflow) ─────────────────────────────────────────────────
		if (equippedManaPotion2 == null)
		{
			equippedManaPotion2 = potion;
			manaPotionCount2    = 1;
			Debug.Log($"[Equipment] {gameObject.name} picked up {potion.itemName} (slot 2) — 1/{potion.maxStack}");
			return true;
		}
		if (manaPotionCount2 < equippedManaPotion2.maxStack)
		{
			manaPotionCount2++;
			Debug.Log($"[Equipment] {gameObject.name} stacked {potion.itemName} (slot 2) — {manaPotionCount2}/{equippedManaPotion2.maxStack}");
			return true;
		}

		Debug.Log($"[Equipment] {gameObject.name} both mana potion slots full — rejected {potion.itemName}");
		return false;
	}

	/// <summary>
	/// Removes one health potion from slot 1 WITHOUT healing — used by SharePotion to
	/// hand the potion to another hero.  Slot 2 is promoted when slot 1 empties.
	/// Returns the potion SO that was removed, or null if both slots are empty.
	/// </summary>
	public HealthPotionSO GiveHealthPotion()
	{
		if (equippedHealthPotion == null || healthPotionCount <= 0) return null;

		HealthPotionSO given = equippedHealthPotion;
		healthPotionCount--;

		// Promote slot 2 → slot 1 when slot 1 empties
		if (healthPotionCount <= 0)
		{
			equippedHealthPotion  = equippedHealthPotion2;
			healthPotionCount     = healthPotionCount2;
			equippedHealthPotion2 = null;
			healthPotionCount2    = 0;
		}

		Debug.Log($"[Equipment] {gameObject.name} gave away 1× {given.itemName} — " +
		          $"slot 1 now: {healthPotionCount}");
		return given;
	}

	/// <summary>
	/// Removes one mana potion from slot 1 WITHOUT restoring mana — used by SharePotion.
	/// Slot 2 is promoted when slot 1 empties.
	/// Returns the potion SO that was removed, or null if both slots are empty.
	/// </summary>
	public ManaPotionSO GiveManaPotion()
	{
		if (equippedManaPotion == null || manaPotionCount <= 0) return null;

		ManaPotionSO given = equippedManaPotion;
		manaPotionCount--;

		// Promote slot 2 → slot 1 when slot 1 empties
		if (manaPotionCount <= 0)
		{
			equippedManaPotion  = equippedManaPotion2;
			manaPotionCount     = manaPotionCount2;
			equippedManaPotion2 = null;
			manaPotionCount2    = 0;
		}

		Debug.Log($"[Equipment] {gameObject.name} gave away 1× {given.itemName} — " +
		          $"slot 1 now: {manaPotionCount}");
		return given;
	}

	/// <summary>Total health potions across both slots.</summary>
	public int TotalHealthPotions => healthPotionCount + healthPotionCount2;

	/// <summary>Total mana potions across both slots.</summary>
	public int TotalManaPotions => manaPotionCount + manaPotionCount2;

	/// <summary>
	/// Consumes one health potion from slot 1. When slot 1 empties, slot 2 is
	/// promoted into slot 1 automatically so the hero keeps a continuous supply.
	/// Returns true if a potion was consumed; false if both slots are empty.
	/// </summary>
	public bool ConsumeHealthPotion()
	{
		if (equippedHealthPotion == null || healthPotionCount <= 0) return false;

		float heal = equippedHealthPotion.healAmount;
		healthComp?.Heal(heal, gameObject);

		healthPotionCount--;
		Debug.Log($"[Equipment] {gameObject.name} drank health potion (+{heal} HP) — " +
		          $"slot 1: {healthPotionCount} remaining");

		// When slot 1 is drained, promote slot 2 → slot 1
		if (healthPotionCount <= 0)
		{
			equippedHealthPotion  = equippedHealthPotion2;
			healthPotionCount     = healthPotionCount2;
			equippedHealthPotion2 = null;
			healthPotionCount2    = 0;

			if (equippedHealthPotion != null)
				Debug.Log($"[Equipment] {gameObject.name} slot 2 promoted to slot 1 ({healthPotionCount} potions)");
		}

		return true;
	}

	/// <summary>
	/// Consumes one mana potion from slot 1. When slot 1 empties, slot 2 is
	/// promoted into slot 1 automatically.
	/// Returns true if a potion was consumed; false if both slots are empty.
	/// </summary>
	public bool ConsumeManaPotion()
	{
		if (equippedManaPotion == null || manaPotionCount <= 0) return false;

		float manaRestored = equippedManaPotion.manaAmount;
		var mana = GetComponent<ManaComponent>();
		mana?.AddMana(manaRestored);

		manaPotionCount--;
		Debug.Log($"[Equipment] {gameObject.name} drank mana potion (+{manaRestored} mana) — " +
		          $"slot 1: {manaPotionCount} remaining");

		// When slot 1 is drained, promote slot 2 → slot 1
		if (manaPotionCount <= 0)
		{
			equippedManaPotion  = equippedManaPotion2;
			manaPotionCount     = manaPotionCount2;
			equippedManaPotion2 = null;
			manaPotionCount2    = 0;

			if (equippedManaPotion != null)
				Debug.Log($"[Equipment] {gameObject.name} slot 2 promoted to slot 1 ({manaPotionCount} potions)");
		}

		return true;
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
				  $"Range: {damageComp?.AttackRange}, HealBonus: {newWeapon.healingBonus}");
	}

	private void ForceEquipHead(HeadArmorSO newHead)
	{
		if (equippedHead != null)
			healthComp?.AddArmorBonus(-appliedHeadArmor);

		equippedHead     = newHead;
		appliedHeadArmor = newHead.statValue;
		healthComp?.AddArmorBonus(appliedHeadArmor);

		Debug.Log($"[Equipment] {gameObject.name} equipped {newHead.itemName} → " +
				  $"{healthComp?.DamageReduction:P0} total reduction");
	}

	private void ForceEquipBody(BodyArmorSO newBody)
	{
		if (equippedBody != null)
			healthComp?.AddArmorBonus(-appliedBodyArmor);

		equippedBody     = newBody;
		appliedBodyArmor = newBody.statValue;
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
		string hpSlot1 = equippedHealthPotion  != null ? $"{equippedHealthPotion.itemName} x{healthPotionCount}"   : "none";
		string hpSlot2 = equippedHealthPotion2 != null ? $"{equippedHealthPotion2.itemName} x{healthPotionCount2}" : "none";
		string mpSlot1 = equippedManaPotion    != null ? $"{equippedManaPotion.itemName} x{manaPotionCount}"       : "none";
		string mpSlot2 = equippedManaPotion2   != null ? $"{equippedManaPotion2.itemName} x{manaPotionCount2}"     : "none";

		Debug.Log($"[{gameObject.name} Loadout] " +
				  $"Weapon: {(equippedWeapon != null ? equippedWeapon.itemName : "none")} | " +
				  $"Head: {(equippedHead   != null ? equippedHead.itemName   : "none")} | " +
				  $"Body: {(equippedBody   != null ? equippedBody.itemName   : "none")} | " +
				  $"Relic: {(equippedRelic  != null ? equippedRelic.itemName  : "none")} | " +
				  $"HP Pot [1]: {hpSlot1} [2]: {hpSlot2} | " +
				  $"Mana Pot [1]: {mpSlot1} [2]: {mpSlot2}");
	}

	private void OnDisable()
	{
		// Potions are not dropped on death/disable — they're consumed silently
	}
}
