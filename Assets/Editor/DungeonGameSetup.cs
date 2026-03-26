// DungeonGameSetup.cs  —  place in any folder named "Editor"
// Adds two menu items under  Tools ▶ Dungeon Game:
//
//   ① Create Party Equipment Panel
//        Builds a fully-wired side-panel Canvas hierarchy in the open scene
//        and attaches PartyEquipmentPanelUI with all four HeroEquipmentUI
//        slots pre-configured.
//
//   ② Add Name Labels to Selected Hero Prefabs
//        Opens each selected hero prefab, adds a world-space "NameLabel"
//        Canvas with a UnitNameUI component, then saves the prefab.
//        Works on prefab assets in the Project window or GameObjects in the
//        scene (prefab instances are edited via PrefabUtility).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public static class DungeonGameSetup
{
    // ═══════════════════════════════════════════════════════════════════════
    //  ①  PARTY EQUIPMENT PANEL
    // ═══════════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Dungeon Game/Create Party Equipment Panel")]
    static void CreatePartyEquipmentPanel()
    {
        Canvas hud = FindOrCreateHUDCanvas();

        // ── Outer panel (anchored to right edge, vertical stack) ───────────
        GameObject panelGO = new GameObject("PartyEquipmentPanel");
        Undo.RegisterCreatedObjectUndo(panelGO, "Create Party Equipment Panel");
        GameObjectUtility.SetParentAndAlign(panelGO, hud.gameObject);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        // Anchor to the right-centre of the screen with a small inset
        panelRT.anchorMin        = new Vector2(1f, 0.5f);
        panelRT.anchorMax        = new Vector2(1f, 0.5f);
        panelRT.pivot            = new Vector2(1f, 0.5f);
        panelRT.anchoredPosition = new Vector2(-12f, 0f);
        panelRT.sizeDelta        = new Vector2(200f, 0f);   // height driven by content

        Image panelBG = panelGO.AddComponent<Image>();
        panelBG.color = new Color(0f, 0f, 0f, 0.6f);

        VerticalLayoutGroup vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 6f;
        vlg.padding              = new RectOffset(6, 6, 6, 6);
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter panelCSF = panelGO.AddComponent<ContentSizeFitter>();
        panelCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        PartyEquipmentPanelUI panelUI = panelGO.AddComponent<PartyEquipmentPanelUI>();

        // ── Four HeroEquipmentUI slots ─────────────────────────────────────
        var slotList = new List<HeroEquipmentUI>();
        for (int i = 0; i < 4; i++)
            slotList.Add(BuildEquipmentSlot(panelGO.transform, i));

        // Wire slots into PartyEquipmentPanelUI via serialised property
        SerializedObject panelSO   = new SerializedObject(panelUI);
        SerializedProperty slotArr = panelSO.FindProperty("slots");
        slotArr.arraySize = slotList.Count;
        for (int i = 0; i < slotList.Count; i++)
            slotArr.GetArrayElementAtIndex(i).objectReferenceValue = slotList[i];
        panelSO.ApplyModifiedProperties();

        Selection.activeGameObject = panelGO;
        Debug.Log("[DungeonGameSetup] Party Equipment Panel created — select it in the " +
                  "Hierarchy to inspect, then enter Play Mode to see it populate.");
    }

    // ── Builds one hero slot (player label + four item slots) ─────────────
    static HeroEquipmentUI BuildEquipmentSlot(Transform parent, int playerIndex)
    {
        // Slot root
        GameObject slotGO = new GameObject($"HeroSlot_P{playerIndex + 1}");
        GameObjectUtility.SetParentAndAlign(slotGO, parent.gameObject);

        slotGO.AddComponent<RectTransform>();
        Image slotBG = slotGO.AddComponent<Image>();
        slotBG.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

        VerticalLayoutGroup slotVLG = slotGO.AddComponent<VerticalLayoutGroup>();
        slotVLG.spacing              = 3f;
        slotVLG.padding              = new RectOffset(4, 4, 4, 4);
        slotVLG.childControlWidth    = true;
        slotVLG.childControlHeight   = true;
        slotVLG.childForceExpandWidth  = true;
        slotVLG.childForceExpandHeight = false;

        ContentSizeFitter slotCSF = slotGO.AddComponent<ContentSizeFitter>();
        slotCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        HeroEquipmentUI heroUI = slotGO.AddComponent<HeroEquipmentUI>();

        // Player label ("Player 1" … "Player 4")
        TextMeshProUGUI labelTMP = CreateTMPLabel(slotGO.transform, $"PlayerLabel_P{playerIndex + 1}",
                                                  $"Player {playerIndex + 1}", 14, FontStyles.Bold,
                                                  TextAlignmentOptions.Center, new Vector2(0f, 24f));

        // Wire the label into HeroEquipmentUI
        SerializedObject heroSO = new SerializedObject(heroUI);
        heroSO.FindProperty("playerLabel").objectReferenceValue = labelTMP;

        // Four item slots in a horizontal row
        GameObject rowGO = new GameObject("ItemRow");
        GameObjectUtility.SetParentAndAlign(rowGO, slotGO);
        rowGO.AddComponent<RectTransform>();

        HorizontalLayoutGroup rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
        rowHLG.spacing              = 4f;
        rowHLG.childAlignment       = TextAnchor.MiddleCenter;
        rowHLG.childControlWidth    = false;
        rowHLG.childControlHeight   = true;
        rowHLG.childForceExpandWidth  = false;
        rowHLG.childForceExpandHeight = false;

        ContentSizeFitter rowCSF = rowGO.AddComponent<ContentSizeFitter>();
        rowCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        rowCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        string[]     slotNames = { "Weapon",  "Head",   "Body",   "Relic"   };
        string[]     propIcons = { "weaponIcon", "headIcon", "bodyIcon", "relicIcon" };
        string[]     propNames = { "weaponName", "headName", "bodyName", "relicName" };

        for (int s = 0; s < 4; s++)
        {
            var (icon, nameLabel) = BuildItemSlot(rowGO.transform, slotNames[s]);
            heroSO.FindProperty(propIcons[s]).objectReferenceValue = icon;
            heroSO.FindProperty(propNames[s]).objectReferenceValue = nameLabel;
        }

        heroSO.ApplyModifiedProperties();
        return heroUI;
    }

    // ── Builds one icon+label column (weapon / head / body / relic) ────────
    static (Image icon, TextMeshProUGUI nameLabel) BuildItemSlot(Transform parent, string slotName)
    {
        GameObject cell = new GameObject(slotName + "Slot");
        GameObjectUtility.SetParentAndAlign(cell, parent.gameObject);

        RectTransform cellRT = cell.AddComponent<RectTransform>();
        cellRT.sizeDelta = new Vector2(40f, 0f);

        VerticalLayoutGroup cellVLG = cell.AddComponent<VerticalLayoutGroup>();
        cellVLG.spacing              = 2f;
        cellVLG.childControlWidth    = true;
        cellVLG.childControlHeight   = false;
        cellVLG.childForceExpandWidth  = true;
        cellVLG.childForceExpandHeight = false;
        cellVLG.childAlignment       = TextAnchor.UpperCenter;

        ContentSizeFitter cellCSF = cell.AddComponent<ContentSizeFitter>();
        cellCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Icon image
        GameObject iconGO = new GameObject("Icon");
        GameObjectUtility.SetParentAndAlign(iconGO, cell);
        RectTransform iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.sizeDelta = new Vector2(36f, 36f);
        Image icon = iconGO.AddComponent<Image>();
        icon.color = new Color(0.3f, 0.3f, 0.3f, 1f);   // grey placeholder until runtime

        LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.minWidth    = 36f;
        iconLE.minHeight   = 36f;
        iconLE.preferredWidth  = 36f;
        iconLE.preferredHeight = 36f;

        // Name label
        TextMeshProUGUI nameLabel = CreateTMPLabel(cell.transform, "Name", "Empty",
                                                   9, FontStyles.Normal,
                                                   TextAlignmentOptions.Center,
                                                   new Vector2(0f, 16f));

        return (icon, nameLabel);
    }

    // ── Creates a TextMeshProUGUI child with common settings ───────────────
    static TextMeshProUGUI CreateTMPLabel(Transform parent, string goName, string text,
                                          float fontSize, FontStyles style,
                                          TextAlignmentOptions alignment, Vector2 preferredSize)
    {
        GameObject go = new GameObject(goName);
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = preferredSize;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text           = text;
        tmp.fontSize       = fontSize;
        tmp.fontStyle      = style;
        tmp.alignment      = alignment;
        tmp.color          = Color.white;
        tmp.overflowMode   = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = false;

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredSize.y;

        return tmp;
    }

    // ── Finds the scene Canvas named "HUDCanvas", or creates one ──────────
    static Canvas FindOrCreateHUDCanvas()
    {
        // Prefer one already named HUDCanvas
        var all = Object.FindObjectsOfType<Canvas>();
        foreach (var c in all)
            if (c.name == "HUDCanvas") return c;

        // Fall back to any screen-space overlay canvas
        foreach (var c in all)
            if (c.renderMode == RenderMode.ScreenSpaceOverlay) return c;

        // Nothing suitable — create one from scratch
        GameObject canvasGO = new GameObject("HUDCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD Canvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // EventSystem (needed for UI interaction, create if missing)
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject evtGO = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(evtGO, "Create EventSystem");
            evtGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            evtGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Debug.Log("[DungeonGameSetup] Created new HUDCanvas.");
        return canvas;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ②  NAME LABELS ON HERO PREFABS
    // ═══════════════════════════════════════════════════════════════════════

    [MenuItem("Tools/Dungeon Game/Add Name Labels to Selected Hero Prefabs")]
    static void AddNameLabels()
    {
        int modified = 0;

        foreach (GameObject selected in Selection.gameObjects)
        {
            // Decide whether we're editing a prefab asset or a scene instance
            string prefabPath = AssetDatabase.GetAssetPath(selected);
            bool isPrefabAsset = !string.IsNullOrEmpty(prefabPath);

            if (isPrefabAsset)
            {
                // Edit the prefab asset directly
                GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (AddNameLabelToRoot(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    modified++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                // Scene instance — edit and apply back to prefab if connected
                if (AddNameLabelToRoot(selected))
                {
                    Undo.RegisterFullObjectHierarchyUndo(selected, "Add Name Label");
                    modified++;

                    string instancePrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected);
                    if (!string.IsNullOrEmpty(instancePrefabPath))
                        PrefabUtility.ApplyPrefabInstance(selected, InteractionMode.UserAction);
                }
            }
        }

        if (modified == 0)
            Debug.LogWarning("[DungeonGameSetup] No valid hero GameObjects selected. " +
                             "Select prefab assets or scene instances with a BaseHero component.");
        else
            Debug.Log($"[DungeonGameSetup] Name labels added/verified on {modified} hero(es).");
    }

    [MenuItem("Tools/Dungeon Game/Add Name Labels to Selected Hero Prefabs", true)]
    static bool AddNameLabelsValidation() => Selection.gameObjects.Length > 0;

    // ── Core logic: adds the world-space label hierarchy if not already present ──
    static bool AddNameLabelToRoot(GameObject root)
    {
        if (root.GetComponent<BaseHero>() == null)
        {
            Debug.LogWarning($"[DungeonGameSetup] '{root.name}' has no BaseHero component — skipping.");
            return false;
        }

        // Don't add a second label if one already exists
        if (root.GetComponentInChildren<UnitNameUI>() != null)
        {
            Debug.Log($"[DungeonGameSetup] '{root.name}' already has a UnitNameUI — skipping.");
            return false;
        }

        // World-space canvas parent
        GameObject canvasGO = new GameObject("NameLabel");
        canvasGO.transform.SetParent(root.transform, false);

        Canvas wsCan = canvasGO.AddComponent<Canvas>();
        wsCan.renderMode = RenderMode.WorldSpace;

        RectTransform canRT = canvasGO.GetComponent<RectTransform>();
        canRT.sizeDelta        = new Vector2(2f, 0.4f);    // 2 world-units wide, 0.4 tall
        canRT.localPosition    = new Vector3(0f, 0.85f, 0f);  // above the sprite
        canRT.localScale       = Vector3.one * 0.01f;      // scale down to world-unit size

        // TMP text child
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin  = Vector2.zero;
        textRT.anchorMax  = Vector2.one;
        textRT.offsetMin  = Vector2.zero;
        textRT.offsetMax  = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text           = "Player ?";   // UnitNameUI will fill this at runtime
        tmp.fontSize       = 6f;
        tmp.fontStyle      = FontStyles.Bold;
        tmp.alignment      = TextAlignmentOptions.Center;
        tmp.color          = Color.white;
        tmp.enableWordWrapping = false;
        tmp.overflowMode   = TextOverflowModes.Overflow;

        // Add outline for readability over any background
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;

        // UnitNameUI on the canvas root, pointing at the text
        UnitNameUI nameUI = canvasGO.AddComponent<UnitNameUI>();
        SerializedObject nameUISO = new SerializedObject(nameUI);
        nameUISO.FindProperty("nameLabel").objectReferenceValue = tmp;
        nameUISO.ApplyModifiedProperties();

        return true;
    }
}
