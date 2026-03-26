using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Side-panel UI manager that distributes spawned heroes across up to 4
/// HeroEquipmentUI slots arranged vertically on the edge of the screen.
///
/// HOW TO SET UP IN THE EDITOR:
///   1. Create a Screen-Space Canvas (or use your existing HUD canvas).
///   2. Add a vertical Panel anchored to the left or right screen edge.
///   3. Inside that panel, create up to 4 child GameObjects, each with a
///      HeroEquipmentUI component (with its Image/Text slots wired up).
///   4. Drag those HeroEquipmentUI children into the "Slots" list here.
///   5. The panel slots start hidden; they activate automatically when
///      heroes spawn and are bound to their matching party member.
/// </summary>
public class PartyEquipmentPanelUI : MonoBehaviour
{
    [Header("Equipment Slots (one per party member, ordered top to bottom)")]
    [Tooltip("Up to 4 HeroEquipmentUI panels. Each slot is shown/hidden based on party size.")]
    [SerializeField] private List<HeroEquipmentUI> slots;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void OnEnable()  => DungeonSpawner.OnPartySpawned += OnPartySpawned;
    private void OnDisable() => DungeonSpawner.OnPartySpawned -= OnPartySpawned;

    private void Awake()
    {
        // Hide every slot until heroes are actually spawned
        foreach (var slot in slots)
            if (slot != null) slot.gameObject.SetActive(false);
    }

    // ── Event Handler ───────────────────────────────────────────────────────

    private void OnPartySpawned(List<GameObject> heroes)
    {
        // Reset — hide all, then re-enable only the ones we need
        foreach (var slot in slots)
            if (slot != null) slot.gameObject.SetActive(false);

        for (int i = 0; i < heroes.Count && i < slots.Count; i++)
        {
            if (heroes[i] == null || slots[i] == null) continue;

            var equipment = heroes[i].GetComponent<EquipmentComponent>();
            if (equipment == null)
            {
                Debug.LogWarning($"[PartyEquipmentPanelUI] Hero {i} has no EquipmentComponent — slot hidden.");
                continue;
            }

            slots[i].gameObject.SetActive(true);
            slots[i].SetHero(equipment, i);
        }
    }
}
