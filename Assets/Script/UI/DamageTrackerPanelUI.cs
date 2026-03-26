using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Side-panel UI manager that shows each hero's cumulative damage dealt
/// alongside the party equipment panel.
///
/// Mirrors the structure of PartyEquipmentPanelUI — one pre-created
/// HeroDamageRowUI slot per possible party member, hidden by default and
/// activated when heroes spawn.
///
/// HOW TO SET UP IN THE EDITOR:
///   1. Add a vertical Panel to your HUD Canvas (e.g. right-side column,
///      or as a second column inside the existing equipment panel).
///   2. Give the panel a header label ("Damage") if desired.
///   3. Inside the panel, create up to 4 child GameObjects, each carrying a
///      HeroDamageRowUI component with its TextMeshProUGUI slots wired up.
///   4. Drag those four children into the "Slots" list here.
///   5. Also drag the DamageTracker GameObject into the tracker reference,
///      OR leave it blank and the panel will find the singleton automatically.
///
/// The panel hides all rows on Awake, then re-activates the right number
/// when DungeonSpawner fires OnPartySpawned.
/// </summary>
public class DamageTrackerPanelUI : MonoBehaviour
{
    [Header("Row Slots (one per party member, ordered top to bottom)")]
    [Tooltip("Up to 4 HeroDamageRowUI rows. Each is shown/hidden based on party size.")]
    [SerializeField] private List<HeroDamageRowUI> slots;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void OnEnable()  => DungeonSpawner.OnPartySpawned += OnPartySpawned;
    private void OnDisable() => DungeonSpawner.OnPartySpawned -= OnPartySpawned;

    private void Awake()
    {
        foreach (var slot in slots)
            if (slot != null) slot.gameObject.SetActive(false);
    }

    // ── Event Handler ──────────────────────────────────────────────────────────

    private void OnPartySpawned(List<GameObject> heroes)
    {
        // Hide all rows first, then re-enable only the ones needed.
        foreach (var slot in slots)
            if (slot != null) slot.gameObject.SetActive(false);

        for (int i = 0; i < heroes.Count && i < slots.Count; i++)
        {
            if (heroes[i] == null || slots[i] == null) continue;

            slots[i].gameObject.SetActive(true);
            slots[i].SetHero(heroes[i], i);
        }
    }
}
