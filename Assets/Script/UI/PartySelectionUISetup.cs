using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Programmatically builds the complete party selection UI panel.
///
/// ── How to use ──────────────────────────────────────────────────────────────
///  1. Add this component to any scene GameObject (e.g. your GameManager object).
///  2. Press Play — the panel is created automatically, hidden, and ready.
///  3. This component destroys itself after building (the panel remains).
///
/// ── What gets created ───────────────────────────────────────────────────────
///  • A ScreenSpace-Overlay Canvas (or attaches to your existing one)
///  • The full PartySelectionPanel hierarchy
///  • PartySelectionUI and PartySlotUI components, fully wired
/// ────────────────────────────────────────────────────────────────────────────
/// </summary>
[DefaultExecutionOrder(-50)]
public class PartySelectionUISetup : MonoBehaviour
{
    // ── Colour palette (dark dungeon theme) ─────────────────────────────────
    private static readonly Color C_OVERLAY    = new Color(0.05f, 0.04f, 0.08f, 0.97f);
    private static readonly Color C_PANEL      = new Color(0.11f, 0.10f, 0.16f, 1.00f);
    private static readonly Color C_HEADER_BG  = new Color(0.15f, 0.13f, 0.22f, 1.00f);
    private static readonly Color C_CARD_BG    = new Color(0.17f, 0.16f, 0.24f, 1.00f);
    private static readonly Color C_CARD_HL    = new Color(0.26f, 0.24f, 0.38f, 1.00f);  // hover tint
    private static readonly Color C_SLOT_BG    = new Color(0.13f, 0.12f, 0.19f, 1.00f);
    private static readonly Color C_SLOT_EMPTY = new Color(0.09f, 0.09f, 0.13f, 1.00f);
    private static readonly Color C_DIVIDER    = new Color(0.28f, 0.25f, 0.40f, 1.00f);
    private static readonly Color C_START_BTN  = new Color(0.68f, 0.48f, 0.10f, 1.00f);
    private static readonly Color C_START_HL   = new Color(0.82f, 0.62f, 0.18f, 1.00f);
    private static readonly Color C_REMOVE_BTN = new Color(0.55f, 0.12f, 0.12f, 1.00f);
    private static readonly Color C_REMOVE_HL  = new Color(0.72f, 0.18f, 0.18f, 1.00f);
    private static readonly Color C_ACCENT     = new Color(0.62f, 0.52f, 0.90f, 1.00f);
    private static readonly Color C_TEXT       = new Color(0.93f, 0.91f, 0.88f, 1.00f);
    private static readonly Color C_SUBTEXT    = new Color(0.58f, 0.56f, 0.68f, 1.00f);

    // ── Entry point ─────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureEventSystem();
        EnsurePartyData();
        Canvas canvas = EnsureCanvas();
        Debug.Log($"[PartySelectionUISetup] Building panel on canvas '{canvas.name}' " +
                  $"(renderMode={canvas.renderMode}, sortingOrder={canvas.sortingOrder})");
        BuildPanel(canvas.transform);
        Debug.Log("[PartySelectionUISetup] Panel built successfully.");
        Destroy(this);
    }

    // ── Bootstrap helpers ────────────────────────────────────────────────────

    private static void EnsurePartyData()
    {
        // Use FindFirstObjectByType so we detect the component even if PartyData.Awake()
        // hasn't run yet (Instance would still be null at execution order -50).
        if (FindFirstObjectByType<PartyData>() != null) return;

        // PartyData wasn't placed in the scene manually — create it now.
        // The availableClasses array will be empty; fill it in the Inspector
        // on the generated "PartyData" GameObject to add selectable hero classes.
        var go = new GameObject("PartyData");
        go.AddComponent<PartyData>();
        Debug.Log("[PartySelectionUISetup] Created PartyData automatically. " +
                  "Select the 'PartyData' GameObject and fill in 'Available Classes' in the Inspector.");
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private static Canvas EnsureCanvas()
    {
        Canvas existing = FindFirstObjectByType<Canvas>();
        if (existing != null) return existing;

        var go = new GameObject("UI Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ── Panel assembly ───────────────────────────────────────────────────────

    private void BuildPanel(Transform canvasRoot)
    {
        // ── Root panel (starts inactive so Awake fires only after Configure) ──
        var panel = NewRect("PartySelectionPanel", canvasRoot);
        Stretch(panel);
        AddImage(panel, C_OVERLAY);
        var cg = panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.SetActive(false);

        // ── Title bar ─────────────────────────────────────────────────────────
        var titleBar = NewRect("TitleBar", panel);
        Anchor(titleBar, new Vector2(0f, 0.88f), new Vector2(1f, 1f));
        AddImage(titleBar, C_HEADER_BG);
        AddTMP("TitleText", titleBar, "-- SELECT YOUR PARTY --",
               34, FontStyles.Bold, TextAlignmentOptions.Center, C_ACCENT,
               Vector2.zero, Vector2.one, new Vector2(0, 0));

        // ── Content row ───────────────────────────────────────────────────────
        var content = NewRect("ContentRow", panel);
        Anchor(content, new Vector2(0.01f, 0.14f), new Vector2(0.99f, 0.86f));

        // Left column — available classes
        var leftCol = NewRect("AvailableClasses", content);
        Anchor(leftCol, Vector2.zero, new Vector2(0.54f, 1f));
        AddImage(leftCol, C_PANEL);
        RoundedBorder(leftCol, C_DIVIDER);

        AddTMP("ClassesHeader", leftCol, "AVAILABLE CLASSES",
               15, FontStyles.Bold, TextAlignmentOptions.Center, C_SUBTEXT,
               new Vector2(0f, 0.93f), new Vector2(1f, 1f), new Vector2(0, -2));

        var divL = NewRect("DividerL", leftCol);
        Anchor(divL, new Vector2(0.03f, 0.905f), new Vector2(0.97f, 0.910f));
        AddImage(divL, C_DIVIDER);

        // Simple card grid — no ScrollRect/Mask so cards are always visible.
        // A plain RectTransform with GridLayoutGroup is reliable; a masked
        // ScrollRect+ContentSizeFitter combo is unreliable when built at runtime.
        var cardContainer = NewRect("ClassCardGrid", leftCol);
        Anchor(cardContainer, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.90f));
        var classCardParent = cardContainer;

        var grid = cardContainer.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(130, 150);
        grid.spacing         = new Vector2(10, 10);
        grid.padding         = new RectOffset(12, 12, 12, 12);
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperLeft;
        grid.constraint      = GridLayoutGroup.Constraint.Flexible;

        // Right column — party slots
        var rightCol = NewRect("PartySlots", content);
        Anchor(rightCol, new Vector2(0.56f, 0f), new Vector2(1f, 1f));
        AddImage(rightCol, C_PANEL);
        RoundedBorder(rightCol, C_DIVIDER);

        AddTMP("PartyHeader", rightCol, "YOUR PARTY",
               15, FontStyles.Bold, TextAlignmentOptions.Center, C_SUBTEXT,
               new Vector2(0f, 0.93f), new Vector2(1f, 1f), new Vector2(0, -2));

        var divR = NewRect("DividerR", rightCol);
        Anchor(divR, new Vector2(0.03f, 0.905f), new Vector2(0.97f, 0.910f));
        AddImage(divR, C_DIVIDER);

        var slots = new PartySlotUI[4];
        for (int i = 0; i < 4; i++)
        {
            float top    = 0.895f - i * 0.222f;
            float bottom = top    - 0.200f;
            slots[i] = BuildPartySlot($"Slot_{i}", rightCol, top, bottom);
        }

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = NewRect("Footer", panel);
        Anchor(footer, new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.13f));
        AddImage(footer, C_HEADER_BG);
        RoundedBorder(footer, C_DIVIDER);

        var countLabel = AddTMP("PartyCountLabel", footer, "0 / 4 heroes selected",
            18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, C_SUBTEXT,
            new Vector2(0.02f, 0.1f), new Vector2(0.45f, 0.9f), new Vector2(10, 0));

        var startBtn = BuildButton("StartButton", footer,
            "START EXPEDITION",
            C_START_BTN, C_START_HL, C_TEXT, 20, FontStyles.Bold,
            new Vector2(0.50f, 0.12f), new Vector2(0.97f, 0.88f));

        // ── Card template (inactive — used as instantiation source) ──────────
        var cardTemplate = BuildCardTemplate(panel);

        // ── Wire up PartySelectionUI ──────────────────────────────────────────
        var ui = panel.gameObject.AddComponent<PartySelectionUI>();
        ui.Configure(classCardParent, cardTemplate, slots, startBtn, countLabel, cg);

        // Activating the panel fires Awake on all components (including PartySelectionUI)
        panel.gameObject.SetActive(true);
    }

    // ── Party slot builder ───────────────────────────────────────────────────

    private PartySlotUI BuildPartySlot(string name, RectTransform parent, float top, float bottom)
    {
        var slot = NewRect(name, parent);
        Anchor(slot, new Vector2(0.03f, bottom), new Vector2(0.97f, top));
        AddImage(slot, C_SLOT_BG);

        // Icon (left side)
        var iconRect = NewRect("Icon", slot);
        Anchor(iconRect, new Vector2(0.02f, 0.10f), new Vector2(0.22f, 0.90f));
        var iconImg = iconRect.gameObject.AddComponent<Image>();
        iconImg.color = Color.white;

        // Name label (centre)
        var nameLabel = AddTMP("HeroName", slot, string.Empty,
            16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, C_TEXT,
            new Vector2(0.24f, 0.20f), new Vector2(0.72f, 0.80f), Vector2.zero);

        // Empty overlay (shown when slot is empty)
        var emptyGO = NewRect("EmptyOverlay", slot).gameObject;
        Stretch(emptyGO.GetComponent<RectTransform>());
        AddImage(emptyGO.GetComponent<RectTransform>(), C_SLOT_EMPTY);
        AddTMP("EmptyText", emptyGO.GetComponent<RectTransform>(), "─  EMPTY  ─",
               14, FontStyles.Normal, TextAlignmentOptions.Center, C_SUBTEXT,
               Vector2.zero, Vector2.one, Vector2.zero);

        // Remove button (right side)
        var removeBtn = BuildButton("RemoveButton", slot, "X",
            C_REMOVE_BTN, C_REMOVE_HL, C_TEXT, 18, FontStyles.Bold,
            new Vector2(0.74f, 0.18f), new Vector2(0.96f, 0.82f));
        removeBtn.gameObject.SetActive(false);

        var comp = slot.gameObject.AddComponent<PartySlotUI>();
        comp.Configure(iconImg, nameLabel, removeBtn, emptyGO);
        return comp;
    }

    // ── Card template builder ────────────────────────────────────────────────

    private GameObject BuildCardTemplate(RectTransform parent)
    {
        var card = NewRect("_CardTemplate", parent).gameObject;
        card.SetActive(false);   // inactive — it's just a template

        // Background + hover colours
        var img = card.AddComponent<Image>();
        img.color = C_CARD_BG;

        var btn = card.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = C_CARD_BG;
        colors.highlightedColor = C_CARD_HL;
        colors.pressedColor     = new Color(0.32f, 0.30f, 0.46f, 1f);
        colors.selectedColor    = C_CARD_HL;
        btn.colors = colors;
        btn.targetGraphic = img;

        // Icon (upper two thirds)
        var iconRect = NewRect("Icon", card.GetComponent<RectTransform>());
        Anchor(iconRect, new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.90f));
        var iconImg = iconRect.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;

        // Class name label (lower third)
        AddTMP("Label", card.GetComponent<RectTransform>(), "CLASS",
               13, FontStyles.Bold, TextAlignmentOptions.Center, C_TEXT,
               new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.30f), Vector2.zero);

        // Thin accent line at top
        var accent = NewRect("Accent", card.GetComponent<RectTransform>());
        Anchor(accent, new Vector2(0.1f, 0.93f), new Vector2(0.9f, 0.96f));
        AddImage(accent, C_ACCENT);

        return card;
    }

    // ── ScrollView builder ───────────────────────────────────────────────────

    private struct ScrollViewParts { public RectTransform root; public RectTransform content; }

    private ScrollViewParts BuildScrollView(string name, RectTransform parent,
                                             Vector2 anchorMin, Vector2 anchorMax)
    {
        var root = NewRect(name, parent);
        Anchor(root, anchorMin, anchorMax);

        var viewport = NewRect("Viewport", root);
        Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot     = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        var sr = root.gameObject.AddComponent<ScrollRect>();
        sr.viewport         = viewport;
        sr.content          = content;
        sr.horizontal       = false;
        sr.vertical         = true;
        sr.scrollSensitivity = 20f;
        sr.movementType     = ScrollRect.MovementType.Clamped;

        return new ScrollViewParts { root = root, content = content };
    }

    // ── Button builder ───────────────────────────────────────────────────────

    private Button BuildButton(string name, RectTransform parent, string text,
                                Color normal, Color highlight, Color textColor,
                                float fontSize, FontStyles fontStyle,
                                Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = NewRect(name, parent);
        Anchor(rect, anchorMin, anchorMax);

        var img = rect.gameObject.AddComponent<Image>();
        img.color = normal;

        var btn = rect.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = normal;
        colors.highlightedColor = highlight;
        colors.pressedColor     = new Color(highlight.r * 0.8f, highlight.g * 0.8f, highlight.b * 0.8f, 1f);
        colors.selectedColor    = highlight;
        btn.colors = colors;
        btn.targetGraphic = img;

        AddTMP(name + "Label", rect, text, fontSize, fontStyle,
               TextAlignmentOptions.Center, textColor,
               new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), Vector2.zero);

        return btn;
    }

    // ── RectTransform helpers ────────────────────────────────────────────────

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    // Overload accepting a Transform parent (for canvas root, etc.)
    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── Image helper ─────────────────────────────────────────────────────────

    private static Image AddImage(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // Thin 1-pixel border overlay
    private static void RoundedBorder(RectTransform rt, Color color)
    {
        var border = NewRect("Border", rt);
        Stretch(border);
        var img = border.gameObject.AddComponent<Image>();
        img.color = Color.clear;

        var outline = border.gameObject.AddComponent<Outline>();
        outline.effectColor    = color;
        outline.effectDistance = new Vector2(1, 1);
    }

    // ── TextMeshPro helper ───────────────────────────────────────────────────

    private static TextMeshProUGUI AddTMP(string name, RectTransform parent,
                                           string text, float size, FontStyles style,
                                           TextAlignmentOptions alignment, Color color,
                                           Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 offsetAdj)
    {
        var rt = NewRect(name, parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetAdj;
        rt.offsetMax = -offsetAdj;

        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text           = text;
        tmp.fontSize       = size;
        tmp.fontStyle      = style;
        tmp.alignment      = alignment;
        tmp.color          = color;
        tmp.overflowMode   = TextOverflowModes.Ellipsis;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }
}
