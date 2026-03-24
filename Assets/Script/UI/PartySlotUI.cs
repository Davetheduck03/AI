using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents one of the four party slots in the party selection screen.
/// Can be wired via the Inspector OR configured programmatically by PartySelectionUISetup.
/// </summary>
public class PartySlotUI : MonoBehaviour
{
    [SerializeField] private Image           heroIcon;
    [SerializeField] private TextMeshProUGUI heroNameLabel;
    [SerializeField] private Button          removeButton;
    [SerializeField] private GameObject      emptyOverlay;

    public int SlotIndex { get; private set; }

    private PartySelectionUI _owner;

    // ── Setup ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PartySelectionUISetup after building the hierarchy so references
    /// can be injected without needing Inspector wiring.
    /// </summary>
    public void Configure(Image icon, TextMeshProUGUI nameLabel,
                           Button removeBtn, GameObject emptyObj)
    {
        heroIcon      = icon;
        heroNameLabel = nameLabel;
        removeButton  = removeBtn;
        emptyOverlay  = emptyObj;
    }

    /// <summary>Called by PartySelectionUI.Awake (or after Configure) to finalise setup.</summary>
    public void Init(int slotIndex, PartySelectionUI owner)
    {
        SlotIndex = slotIndex;
        _owner    = owner;

        removeButton?.onClick.AddListener(OnRemoveClicked);
        SetEmpty();
    }

    // ── State ────────────────────────────────────────────────────────────────

    public void SetHero(HeroClassEntry entry)
    {
        if (heroIcon != null)
        {
            heroIcon.sprite  = entry.classSO?.classIcon;
            heroIcon.enabled = entry.classSO?.classIcon != null;
        }

        if (heroNameLabel != null)
            heroNameLabel.text = entry.classSO?.className ?? "Hero";

        if (emptyOverlay != null) emptyOverlay.SetActive(false);
        if (removeButton != null) removeButton.gameObject.SetActive(true);
    }

    public void SetEmpty()
    {
        if (heroIcon != null)      heroIcon.enabled = false;
        if (heroNameLabel != null) heroNameLabel.text = string.Empty;
        if (emptyOverlay != null)  emptyOverlay.SetActive(true);
        if (removeButton != null)  removeButton.gameObject.SetActive(false);
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private void OnRemoveClicked() => _owner?.RemoveHeroAt(SlotIndex);

    private void OnDestroy() => removeButton?.onClick.RemoveAllListeners();
}
