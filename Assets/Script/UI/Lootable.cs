using NUnit.Framework.Internal.Execution;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Lootable : MonoBehaviour
{
	[Header("Loot Settings")]
	[SerializeField] private float lootDuration = 2f;
	[SerializeField] private LootTable lootTable;           // assign in Inspector
	[SerializeField] private GameObject worldItemPrefab;    // your WorldItem prefab

	[Header("UI References")]
	[SerializeField] private GameObject lootBarUI;
	[SerializeField] private Image fillImage;

	public static event Action<Lootable> OnLootComplete;

	private Coroutine lootCoroutine;
	public bool isLooting = false;
	public bool isLooted = false;

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
		if (lootTable == null || worldItemPrefab == null) return;

		// Roll once per loot table entry slot — or just once if you want a single drop
		ItemSO drop = lootTable.Roll();
		if (drop == null) return;

		GameObject obj = Instantiate(worldItemPrefab, transform.position, Quaternion.identity);
		WorldItem worldItem = obj.GetComponent<WorldItem>();
		if (worldItem != null)
			worldItem.item = drop;
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