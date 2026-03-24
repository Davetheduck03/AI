using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the party selection screen logic.
/// References can be wired via the Inspector OR injected via Configure()
/// (called by PartySelectionUISetup when the panel is built programmatically).
/// </summary>
public class PartySelectionUI : MonoBehaviour
{
    public static PartySelectionUI Instance { get; private set; }

    [Header("Class Cards")]
    [SerializeField] private Transform  classCardParent;
    [SerializeField] private GameObject classCardPrefab;

    [Header("Party Slots (assign 4 in order)")]
    [SerializeField] private PartySlotUI[] partySlots;

    [Header("Controls")]
    [SerializeField] private Button          startButton;
    [SerializeField] private TextMeshProUGUI partyCountLabel;

    [Header("Root")]
    [SerializeField] private CanvasGroup rootCanvasGroup;

    // ── Programmatic setup ───────────────────────────────────────────────────

    /// <summary>
    /// Called by PartySelectionUISetup after building the full hierarchy.
    /// Sets all internal references without requiring Inspector wiring.
    /// The GameObject must be inactive when this is called; it will be activated
    /// afterwards, at which point Awake fires and completes initialisation.
    /// </summary>
    public void Configure(Transform cardParent, GameObject cardTemplate,
                           PartySlotUI[] slots, Button startBtn,
                           TextMeshProUGUI countLabel, CanvasGroup cg)
    {
        classCardParent  = cardParent;
        classCardPrefab  = cardTemplate;
        partySlots       = slots;
        startButton      = startBtn;
        partyCountLabel  = countLabel;
        rootCanvasGroup  = cg;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        for (int i = 0; i < partySlots.Length; i++)
            partySlots[i]?.Init(i, this);

        startButton?.onClick.AddListener(OnStartClicked);

        Hide();
    }

    private void OnDestroy()
    {
        startButton?.onClick.RemoveAllListeners();
    }

    // ── Show / Hide ──────────────────────────────────────────────────────────

    public void Show()
    {
        Debug.Log($"[PartySelectionUI] Show() — rootCanvasGroup={(rootCanvasGroup != null ? "SET" : "NULL")}" +
                  $"  classCardParent={(classCardParent != null ? "SET" : "NULL")}" +
                  $"  classCardPrefab={(classCardPrefab != null ? "SET" : "NULL")}" +
                  $"  PartyData.Instance={(PartyData.Instance != null ? "SET" : "NULL")}");
        SetVisible(true);
        BuildClassCards();
        RefreshPartyDisplay();
    }

    public void Hide() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha          = visible ? 1f : 0f;
            rootCanvasGroup.interactable   = visible;
            rootCanvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    // ── Class card building ──────────────────────────────────────────────────

    private void BuildClassCards()
    {
        foreach (Transform child in classCardParent)
            Destroy(child.gameObject);

        if (PartyData.Instance == null)
        {
            Debug.LogWarning("[PartySelectionUI] BuildClassCards — PartyData.Instance is null, no cards built.");
            return;
        }

        int classCount = PartyData.Instance.availableClasses?.Length ?? 0;
        Debug.Log($"[PartySelectionUI] BuildClassCards — building {classCount} class card(s).");

        foreach (var entry in PartyData.Instance.availableClasses)
        {
            if (entry?.classSO == null) continue;

            GameObject card = Instantiate(classCardPrefab, classCardParent);

            var iconImg = card.transform.Find("Icon")?.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.sprite  = entry.classSO.classIcon;
                iconImg.enabled = entry.classSO.classIcon != null;
            }

            var label = card.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.text = entry.classSO.className;

            var btn = card.GetComponent<Button>();
            if (btn != null)
            {
                var captured = entry;
                btn.onClick.AddListener(() => OnClassCardClicked(captured));
            }

            // Template is inactive — activate the clone so it's visible.
            card.SetActive(true);
        }

    }

    // ── Selection callbacks ──────────────────────────────────────────────────

    private void OnClassCardClicked(HeroClassEntry entry)
    {
        if (PartyData.Instance == null) return;

        bool added = PartyData.Instance.AddToParty(entry);
        if (!added)
            Debug.Log("[PartySelectionUI] Party is full (max 4 heroes).");

        RefreshPartyDisplay();
    }

    public void RemoveHeroAt(int slotIndex)
    {
        PartyData.Instance?.RemoveFromParty(slotIndex);
        RefreshPartyDisplay();
    }

    // ── Party display ────────────────────────────────────────────────────────

    private void RefreshPartyDisplay()
    {
        var party = PartyData.Instance?.SelectedParty;
        int count = party?.Count ?? 0;

        for (int i = 0; i < partySlots.Length; i++)
        {
            if (partySlots[i] == null) continue;
            if (i < count) partySlots[i].SetHero(party[i]);
            else           partySlots[i].SetEmpty();
        }

        if (partyCountLabel != null)
            partyCountLabel.text = $"{count} / {PartyData.MaxPartySize} heroes";

        if (startButton != null)
            startButton.interactable = count > 0;
    }

    // ── Start button ─────────────────────────────────────────────────────────

    private void OnStartClicked()
    {
        if (GameManager.Instance?.GetCurrentState() is RoundState_PartySelect ps)
            ps.Confirm();
        else
            Debug.LogWarning("[PartySelectionUI] Start clicked but current state is not RoundState_PartySelect.");
    }
}
