using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Loot Table")]
public class LootTable : ScriptableObject
{
	[System.Serializable]
	public class LootEntry
	{
		public ItemSO item;
		[Range(0f, 100f)] public float weight = 10f;
	}

	public List<LootEntry> entries;
	[Range(0f, 100f)] public float dropChance = 75f; // chance anything drops at all

	public ItemSO Roll()
	{
		if (Random.value * 100f > dropChance) return null;

		float total = 0f;
		foreach (var e in entries) total += e.weight;

		float roll = Random.value * total;
		foreach (var e in entries)
		{
			roll -= e.weight;
			if (roll <= 0f) return e.item;
		}
		return null;
	}
}