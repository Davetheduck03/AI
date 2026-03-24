using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the hero's currently equipped weapon, head armour, and body armour.
/// Attach to a Canvas UI GameObject. Assign the hero via Inspector or let it
/// auto-find the first GameObject tagged "Hero" on Start.
///
/// Each slot needs:
///   - An Image component for the item icon  (leave Source Image blank — filled at runtime)
///   - (Optional) A TextMeshProUGUI for the item name
/// </summary>
public class HeroEquipmentUI : MonoBehaviour
{
    [Header("Hero Reference")]
    [Tooltip("Drag the hero GameObject here, or leave empty to auto-find by tag 'Hero'.")]
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
        if (heroEquipment == null)
        {
            GameObject hero = GameObject.FindGameObjectWithTag("Hero");
            if (hero != null)
                heroEquipment = hero.GetComponent<EquipmentComponent>();

            if (heroEquipment == null)
                Debug.LogWarning("[HeroEquipmentUI] No EquipmentComponent found. " +
                                 "Assign it in the Inspector or tag the hero 'Hero'.");
        }

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
