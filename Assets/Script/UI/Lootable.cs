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

	[Header("UI References")]
	[SerializeField] private GameObject lootBarUI;
	[SerializeField] private Image fillImage;

	public static event Action<Lootable> OnLootComplete;

	private Coroutine lootCoroutine;
	public bool isLooting = false;
	public bool isLooted = false;

	// ─── Runtime Setters (called by DungeonSpawner) ─────────────────────────

	public void SetLootTable(LootTable table)          => lootTable       = table;
	public void SetWorldItemPrefab(GameObject prefab)  => worldItemPrefab = prefab;
	public void SetGuaranteedItem(ItemSO item)         => guaranteedItem  = item;

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

		// Regular loot table roll
		if (lootTable != null)
		{
			ItemSO drop = lootTable.Roll();
			if (drop != null) SpawnWorldItem(drop);
		}
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