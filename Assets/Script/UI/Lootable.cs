using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Lootable : MonoBehaviour
{
	[Header("Loot Settings")]
	[SerializeField] private float lootDuration = 2f;
	[SerializeField] private LootTable lootTable;           // assign in Inspector or set via SetLootTable()
	[SerializeField] private GameObject worldItemPrefab;    // assign in Inspector or set via SetWorldItemPrefab()

	[Tooltip("If set, this item is always spawned in addition to the loot table roll. " +
	         "Set at runtime by DungeonSpawner to guarantee the Relic drops from one chest.")]
	[SerializeField] private ItemSO guaranteedItem;

	[Header("Potion Drop")]
	[Tooltip("25 % chance a potion also drops from this chest. Set via DungeonSpawner.")]
	[Range(0f, 1f)]
	[SerializeField] private float potionDropChance = 0.25f;
	[SerializeField] private HealthPotionSO healthPotionDrop;
	[SerializeField] private ManaPotionSO   manaPotionDrop;

	[Header("UI References")]
	[SerializeField] private GameObject lootBarUI;
	[SerializeField] private Image fillImage;

	public static event Action<Lootable> OnLootComplete;

	private Coroutine lootCoroutine;
	public bool isLooting = false;
	public bool isLooted = false;

	// ─── Claim system ────────────────────────────────────────────────────────
	// Only the hero that claims this chest will walk to and loot it.
	// Other heroes see it as unavailable in FindLootInRange.

	public Transform claimedBy = null;

	/// <summary>
	/// Returns true and claims the chest if it is unclaimed (or already claimed
	/// by <paramref name="hero"/>). Returns false if a different hero owns it.
	/// Also clears stale claims from heroes that have been destroyed.
	/// </summary>
	public bool TryClaim(Transform hero)
	{
		// Evict a stale claim if the claimer was destroyed
		if (claimedBy != null && (claimedBy.gameObject == null || !claimedBy.gameObject.activeInHierarchy))
			claimedBy = null;

		if (claimedBy == null || claimedBy == hero)
		{
			claimedBy = hero;
			return true;
		}
		return false;
	}

	/// <summary>Releases the claim only if <paramref name="hero"/> currently holds it.</summary>
	public void ReleaseClaim(Transform hero)
	{
		if (claimedBy == hero)
			claimedBy = null;
	}

	// ─── Runtime Setters (called by DungeonSpawner) ─────────────────────────

	public void SetLootTable(LootTable table)          => lootTable       = table;
	public void SetWorldItemPrefab(GameObject prefab)  => worldItemPrefab = prefab;
	public void SetGuaranteedItem(ItemSO item)         => guaranteedItem  = item;

	/// <summary>
	/// Wires up the potions that can drop from this chest.
	/// Called by DungeonSpawner at spawn time so the SOs don't need to be
	/// hand-assigned on every chest prefab in the Inspector.
	/// </summary>
	public void SetPotionDrops(HealthPotionSO hp, ManaPotionSO mp)
	{
		healthPotionDrop = hp;
		manaPotionDrop   = mp;
	}

	// ─── Lifecycle ───────────────────────────────────────────────────────────

	private void Start() => HideLootBar();

	public void Loot()
	{
		if (isLooted || isLooting) return;
		lootCoroutine = StartCoroutine(LootCoroutine());
	}

	public void CancelLoot()
	{
		if (lootCoroutine != null)
		{
			StopCoroutine(lootCoroutine);
			lootCoroutine = null;
		}
		isLooting = false;
		HideLootBar();
	}

	private IEnumerator LootCoroutine()
	{
		isLooting = true;
		ShowLootBar();

		float elapsed = 0f;
		while (elapsed < lootDuration)
		{
			elapsed += Time.deltaTime;
			if (fillImage != null)
				fillImage.fillAmount = Mathf.Clamp01(elapsed / lootDuration);
			yield return null;
		}

		if (fillImage != null) fillImage.fillAmount = 1f;
		yield return new WaitForSeconds(0.2f);

		CompleteLoot();
	}

	private void CompleteLoot()
	{
		isLooting = false;
		isLooted = true;
		HideLootBar();
		OnLootComplete?.Invoke(this);
		SpawnLoot();
		Destroy(gameObject);
	}

	private void SpawnLoot()
	{
		if (worldItemPrefab == null) return;

		// Guaranteed item always drops (e.g. the Relic in the special chest)
		if (guaranteedItem != null)
			SpawnWorldItem(guaranteedItem);

		// Regular loot table roll — equipment piece
		if (lootTable != null)
		{
			ItemSO drop = lootTable.Roll();
			if (drop != null) SpawnWorldItem(drop);
		}

		// 25 % chance to also drop one potion (health or mana, chosen randomly)
		if (UnityEngine.Random.value < potionDropChance)
		{
			ItemSO potion = PickRandomPotion();
			if (potion != null)
			{
				SpawnWorldItem(potion);
				Debug.Log($"[Lootable] Bonus potion drop: {potion.itemName}");
			}
		}
	}

	/// <summary>
	/// Returns a random potion from the ones configured for this chest.
	/// Falls back to whichever type is available if only one is assigned.
	/// </summary>
	private ItemSO PickRandomPotion()
	{
		bool hasHP   = healthPotionDrop != null;
		bool hasMana = manaPotionDrop   != null;

		if (hasHP && hasMana) return UnityEngine.Random.value < 0.5f ? (ItemSO)healthPotionDrop : manaPotionDrop;
		if (hasHP)            return healthPotionDrop;
		if (hasMana)          return manaPotionDrop;
		return null;
	}

	private void SpawnWorldItem(ItemSO itemSO)
	{
		// Small scatter so multiple drops don't stack on the same pixel
		Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), UnityEngine.Random.Range(-0.3f, 0.3f), 0);
		GameObject obj = Instantiate(worldItemPrefab, transform.position + offset, Quaternion.identity);
		WorldItem worldItem = obj.GetComponent<WorldItem>();
		if (worldItem != null)
			worldItem.item = itemSO;
	}

	private void ShowLootBar()
	{
		if (lootBarUI != null) lootBarUI.SetActive(true);
		else Debug.LogWarning("[Lootable] Loot Bar UI not assigned!");
	}

	private void HideLootBar()
	{
		if (lootBarUI != null) lootBarUI.SetActive(false);
		if (fillImage != null) fillImage.fillAmount = 0f;
	}

	private void OnDestroy()
	{
		if (lootCoroutine != null) StopCoroutine(lootCoroutine);
	}

	[ContextMenu("Test Loot")]
	private void TestLoot() => Loot();
}