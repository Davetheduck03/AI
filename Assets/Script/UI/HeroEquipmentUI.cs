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

    [Header("Health Potion Slots")]
    [SerializeField] private Image           hpPotion1Icon;
    [SerializeField] private TextMeshProUGUI hpPotion1Count;
    [SerializeField] private Image           hpPotion2Icon;
    [SerializeField] private TextMeshProUGUI hpPotion2Count;

    [Header("Mana Potion Slots")]
    [SerializeField] private Image           manaPotion1Icon;
    [SerializeField] private TextMeshProUGUI manaPotion1Count;
    [SerializeField] private Image           manaPotion2Icon;
    [SerializeField] private TextMeshProUGUI manaPotion2Count;

    [Header("Empty Slot")]
    [Tooltip("Sprite shown when a slot has no item equipped.")]
    [SerializeField] private Sprite emptySlotSprite;

    [Header("Damage Tracker")]
    [Tooltip("(Optional) TextMeshProUGUI that shows this hero's total damage dealt. " +
             "Leave empty to disable the damage display.")]
    [SerializeField] private TextMeshProUGUI damageLabel;

    // The hero GameObject resolved from the EquipmentComponent — used for DamageTracker lookups.
    private GameObject _heroGO;

    // Cache last-known items so we only redraw when something actually changes
    private ItemSO _lastWeapon;
    private ItemSO _lastHead;
    private ItemSO _lastBody;
    private ItemSO _lastRelic;

    // Potion caches — store SO + count so a stack change also triggers a redraw
    private HealthPotionSO _lastHP1;   private int _lastHP1Count;
    private HealthPotionSO _lastHP2;   private int _lastHP2Count;
    private ManaPotionSO   _lastMP1;   private int _lastMP1Count;
    private ManaPotionSO   _lastMP2;   private int _lastMP2Count;

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

        // ── Potion slots ──────────────────────────────────────────────────────
        if (heroEquipment.equippedHealthPotion  != _lastHP1 || heroEquipment.healthPotionCount  != _lastHP1Count)
        {
            _lastHP1 = heroEquipment.equippedHealthPotion;  _lastHP1Count = heroEquipment.healthPotionCount;
            RefreshPotionSlot(hpPotion1Icon, hpPotion1Count, _lastHP1, _lastHP1Count);
        }
        if (heroEquipment.equippedHealthPotion2 != _lastHP2 || heroEquipment.healthPotionCount2 != _lastHP2Count)
        {
            _lastHP2 = heroEquipment.equippedHealthPotion2; _lastHP2Count = heroEquipment.healthPotionCount2;
            RefreshPotionSlot(hpPotion2Icon, hpPotion2Count, _lastHP2, _lastHP2Count);
        }
        if (heroEquipment.equippedManaPotion    != _lastMP1 || heroEquipment.manaPotionCount    != _lastMP1Count)
        {
            _lastMP1 = heroEquipment.equippedManaPotion;    _lastMP1Count = heroEquipment.manaPotionCount;
            RefreshPotionSlot(manaPotion1Icon, manaPotion1Count, _lastMP1, _lastMP1Count);
        }
        if (heroEquipment.equippedManaPotion2   != _lastMP2 || heroEquipment.manaPotionCount2   != _lastMP2Count)
        {
            _lastMP2 = heroEquipment.equippedManaPotion2;   _lastMP2Count = heroEquipment.manaPotionCount2;
            RefreshPotionSlot(manaPotion2Icon, manaPotion2Count, _lastMP2, _lastMP2Count);
        }

        // Live damage total — poll DamageTracker every frame (cheap dictionary lookup).
        if (damageLabel != null && _heroGO != null)
        {
            float total = DamageTracker.Instance != null
                ? DamageTracker.Instance.GetTotal(_heroGO)
                : 0f;
            damageLabel.text = $"DMG  {total:N0}";
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
        _heroGO       = equipment != null ? equipment.gameObject : null;

        if (playerLabel != null)
            playerLabel.text = $"Player {index + 1}";

        // Reset damage label immediately so stale numbers don't linger between runs.
        if (damageLabel != null)
            damageLabel.text = "DMG  0";

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

        _lastHP1 = heroEquipment.equippedHealthPotion;  _lastHP1Count = heroEquipment.healthPotionCount;
        _lastHP2 = heroEquipment.equippedHealthPotion2; _lastHP2Count = heroEquipment.healthPotionCount2;
        _lastMP1 = heroEquipment.equippedManaPotion;    _lastMP1Count = heroEquipment.manaPotionCount;
        _lastMP2 = heroEquipment.equippedManaPotion2;   _lastMP2Count = heroEquipment.manaPotionCount2;

        RefreshPotionSlot(hpPotion1Icon,   hpPotion1Count,   _lastHP1, _lastHP1Count);
        RefreshPotionSlot(hpPotion2Icon,   hpPotion2Count,   _lastHP2, _lastHP2Count);
        RefreshPotionSlot(manaPotion1Icon, manaPotion1Count, _lastMP1, _lastMP1Count);
        RefreshPotionSlot(manaPotion2Icon, manaPotion2Count, _lastMP2, _lastMP2Count);
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

    /// <summary>
    /// Refreshes a potion slot: shows the potion icon and "xN" stack count,
    /// or switches to the empty sprite with a blank count when the slot is unused.
    /// </summary>
    private void RefreshPotionSlot(Image iconImage, TextMeshProUGUI countLabel, PotionSO potion, int count)
    {
        if (iconImage != null)
            iconImage.sprite = potion != null ? potion.Icon : emptySlotSprite;

        if (countLabel != null)
            countLabel.text = potion != null && count > 0 ? $"x{count}" : string.Empty;
    }
}
