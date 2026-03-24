using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pairs an AdventurerClassSO (stats, allowed weapons, starting weapon) with the
/// prefab that should be instantiated when a hero of that class is spawned.
/// Defined in the Inspector on PartyData.
/// </summary>
[System.Serializable]
public class HeroClassEntry
{
    [Tooltip("The class definition — provides name, icon, allowed weapons, and starting weapon.")]
    public AdventurerClassSO classSO;

    [Tooltip("The hero prefab to instantiate (must have the matching AI component attached).")]
    public GameObject prefab;
}

/// <summary>
/// Singleton that holds:
///   1. The catalogue of classes the player can choose from (configured in the Inspector).
///   2. The currently selected party built in the party selection screen.
///
/// Place this component on a persistent scene object (e.g. alongside GameManager).
/// DungeonSpawner reads SelectedParty to know which heroes to spawn.
/// </summary>
public class PartyData : MonoBehaviour
{
    public static PartyData Instance { get; private set; }

    public const int MaxPartySize = 4;

    [Header("Available Classes")]
    [Tooltip("All classes the player can draft. Add one entry per class.")]
    public HeroClassEntry[] availableClasses;

    // ── Runtime selection — built by PartySelectionUI ──────────────────────

    public IReadOnlyList<HeroClassEntry> SelectedParty => _selectedParty;
    private readonly List<HeroClassEntry> _selectedParty = new List<HeroClassEntry>();

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Selection API ───────────────────────────────────────────────────────

    /// <summary>Adds the entry to the party. Returns false if the party is full.</summary>
    public bool AddToParty(HeroClassEntry entry)
    {
        if (_selectedParty.Count >= MaxPartySize) return false;
        _selectedParty.Add(entry);
        return true;
    }

    /// <summary>Removes the hero at the given slot index.</summary>
    public void RemoveFromParty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _selectedParty.Count) return;
        _selectedParty.RemoveAt(slotIndex);
    }

    /// <summary>Clears all selections.</summary>
    public void ClearParty() => _selectedParty.Clear();

    /// <summary>True if at least one hero has been selected.</summary>
    public bool IsPartyValid() => _selectedParty.Count > 0;
}
