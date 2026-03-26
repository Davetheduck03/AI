using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays one hero's equipped weapon, head armour, body armour, and relic
/// inside a side-panel slot.
///
/// At runtime PartyEquipmentPanelUI calls SetHero() to bind this panel to a
/// specific hero. You no longer need to drag references in the Inspector —
/// the binding happens automatically when heroes are spawned.
///
/// Each slot needs:
///   - An Image component for the item icon  (Source Image filled at runtime)
///   - (Optional) A TextMeshProUGUI for the item name
/// </summary>
public class HeroEquipmentUI : MonoBehaviour
{
    [Header("Player Label")]
    [Tooltip("Text element that shows 'Player 1', 'Player 2', etc.")]
    [SerializeField] private TextMeshProUGUI playerLabel;

    [Header("Hero Reference (set via SetHero() at runtime)")]
    [SerializeField] private EquipmentComponent heroEquipment;

    [Header("Weapon Slot")]
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI weaponName;

    [Header("Head Armour Slot")]
    [SerializeField] private Image headIcon;
    [SerializeField] private TextMeshProUGUI headName;

    [Header("Body Armour Slot")]
    [SerializeField] private Image bodyIcon;
    [SerializeField] private TextMeshProUGUI bodyName;

    [Header("Relic Slot")]
    [SerializeField] private Image relicIcon;
    [SerializeField] private TextMeshProUGUI relicName;

    [Header("Empty Slot")]
    [Tooltip("Sprite shown when a slot has no item equipped.")]
    [SerializeField] private Sprite emptySlotSprite;

    // Cache last-known items so we only redraw when something actually changes
    private ItemSO _lastWeapon;
    private ItemSO _lastHead;
    private ItemSO _lastBody;
    private ItemSO _lastRelic;

    private void Start()
    {
        // If a hero was pre-assigned in the Inspector, initialize straight away.
        // Otherwise wait for SetHero() to be called by PartyEquipmentPanelUI.
        if (heroEquipment != null)
            RefreshAll();
    }

    private void Update()
    {
        if (heroEquipment == null) return;

        // Only redraw the slot that actually changed
        if (heroEquipment.equippedWeapon != _lastWeapon)
        {
            _lastWeapon = heroEquipment.equippedWeapon;
            RefreshSlot(weaponIcon, weaponName, _lastWeapon);
        }

        if (heroEquipment.equippedHead != _lastHead)
        {
            _lastHead = heroEquipment.equippedHead;
            RefreshSlot(headIcon, headName, _lastHead);
        }

        if (heroEquipment.equippedBody != _lastBody)
        {
            _lastBody = heroEquipment.equippedBody;
            RefreshSlot(bodyIcon, bodyName, _lastBody);
        }

        if (heroEquipment.equippedRelic != _lastRelic)
        {
            _lastRelic = heroEquipment.equippedRelic;
            RefreshSlot(relicIcon, relicName, _lastRelic);
        }
    }

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Binds this panel to a specific hero at runtime.
    /// Called by PartyEquipmentPanelUI after heroes are spawned.
    /// </summary>
    public void SetHero(EquipmentComponent equipment, int index)
    {
        heroEquipment = equipment;

        if (playerLabel != null)
            playerLabel.text = $"Player {index + 1}";

        RefreshAll();
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    private void RefreshAll()
    {
        if (heroEquipment == null) return;

        _lastWeapon = heroEquipment.equippedWeapon;
        _lastHead   = heroEquipment.equippedHead;
        _lastBody   = heroEquipment.equippedBody;
        _lastRelic  = heroEquipment.equippedRelic;

        RefreshSlot(weaponIcon, weaponName, _lastWeapon);
        RefreshSlot(headIcon,   headName,   _lastHead);
        RefreshSlot(bodyIcon,   bodyName,   _lastBody);
        RefreshSlot(relicIcon,  relicName,  _lastRelic);
    }

    private void RefreshSlot(Image iconImage, TextMeshProUGUI nameLabel, ItemSO item)
    {
        if (iconImage != null)
        {
            iconImage.sprite  = item != null ? item.Icon : emptySlotSprite;
            iconImage.enabled = true;
        }

        if (nameLabel != null)
            nameLabel.text = item != null ? item.itemName : "Empty";
    }
}
