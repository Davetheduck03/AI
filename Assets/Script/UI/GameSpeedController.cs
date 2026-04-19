using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Cycles game speed with Shift: 1x -> 2x -> 3x -> 1x.
/// Displays the current speed in a panel in the top-right corner.
/// Add this component to any persistent scene object (GameManager, etc.).
/// </summary>
public class GameSpeedController : MonoBehaviour
{
    private static readonly float[] Speeds = { 1f, 2f, 3f };

    private static readonly Color[] SpeedColors =
    {
        new Color(0.85f, 0.85f, 0.85f, 1f),
        new Color(1.00f, 0.85f, 0.20f, 1f),
        new Color(1.00f, 0.45f, 0.10f, 1f),
    };

    private int _index = 0;

    private TextMeshProUGUI _speedText;
    private TextMeshProUGUI _labelText;
    private TextMeshProUGUI _hintText;
    private Image _panelBg;

    // Static so the next Awake (scene reload) can destroy the old canvas.
    private static GameObject _canvasInstance;

    private void Awake()
    {
        if (_canvasInstance != null)
        {
            Destroy(_canvasInstance);
            _canvasInstance = null;
        }

        BuildUI();
        ApplySpeed();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            _index = (_index + 1) % Speeds.Length;
            ApplySpeed();
        }
    }

    private void ApplySpeed()
    {
        Time.timeScale = Speeds[_index];
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (_speedText == null) return;

        float speed = Speeds[_index];
        Color col = SpeedColors[_index];

        string arrows = new string('>', _index + 1);
        _speedText.text = arrows + "  " + speed.ToString("0") + "x";
        _speedText.color = col;

        if (_panelBg != null)
            _panelBg.color = new Color(col.r * 0.15f, col.g * 0.15f, col.b * 0.15f, 0.72f);
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("_SpeedCanvas");
        DontDestroyOnLoad(canvasGO);
        _canvasInstance = canvasGO;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel anchored to top-right: 140 x 76 px
        // Row 1 (top 20 px)    - "GAME SPEED" label
        // Row 2 (middle 36 px) - speed readout
        // Row 3 (bottom 20 px) - "[Shift] to change" hint
        var panelGO = new GameObject("SpeedPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);

        var panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1f, 1f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.pivot = new Vector2(1f, 1f);
        panelRT.anchoredPosition = new Vector2(-14f, -14f);
        panelRT.sizeDelta = new Vector2(140f, 76f);

        _panelBg = panelGO.AddComponent<Image>();
        _panelBg.color = new Color(0f, 0f, 0f, 0.72f);
        _panelBg.raycastTarget = false;

        // Row 1 - header
        var labelGO = new GameObject("SpeedLabel");
        labelGO.transform.SetParent(panelGO.transform, false);

        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0f, 1f);
        labelRT.anchorMax = new Vector2(1f, 1f);
        labelRT.pivot = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, 0f);
        labelRT.sizeDelta = new Vector2(0f, 20f);

        _labelText = labelGO.AddComponent<TextMeshProUGUI>();
        _labelText.text = "GAME SPEED";
        _labelText.fontSize = 11f;
        _labelText.fontStyle = FontStyles.Bold;
        _labelText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        _labelText.alignment = TextAlignmentOptions.Center;
        _labelText.raycastTarget = false;

        // Row 2 - speed readout
        var speedGO = new GameObject("SpeedText");
        speedGO.transform.SetParent(panelGO.transform, false);

        var speedRT = speedGO.AddComponent<RectTransform>();
        speedRT.anchorMin = new Vector2(0f, 1f);
        speedRT.anchorMax = new Vector2(1f, 1f);
        speedRT.pivot = new Vector2(0.5f, 1f);
        speedRT.anchoredPosition = new Vector2(0f, -20f);
        speedRT.sizeDelta = new Vector2(0f, 36f);

        _speedText = speedGO.AddComponent<TextMeshProUGUI>();
        _speedText.fontSize = 24f;
        _speedText.fontStyle = FontStyles.Bold;
        _speedText.alignment = TextAlignmentOptions.Center;
        _speedText.raycastTarget = false;

        // Row 3 - hint
        var hintGO = new GameObject("HintText");
        hintGO.transform.SetParent(panelGO.transform, false);

        var hintRT = hintGO.AddComponent<RectTransform>();
        hintRT.anchorMin = new Vector2(0f, 0f);
        hintRT.anchorMax = new Vector2(1f, 0f);
        hintRT.pivot = new Vector2(0.5f, 0f);
        hintRT.anchoredPosition = new Vector2(0f, 0f);
        hintRT.sizeDelta = new Vector2(0f, 20f);

        _hintText = hintGO.AddComponent<TextMeshProUGUI>();
        _hintText.text = "[Shift] to change";
        _hintText.fontSize = 10f;
        _hintText.color = new Color(0.5f, 0.5f, 0.5f, 0.9f);
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.raycastTarget = false;
    }
}
