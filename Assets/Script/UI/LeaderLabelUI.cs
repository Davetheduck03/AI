using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a "Leader" badge above a hero when they hold the leader slot in
/// FormationManager.  Updates automatically when the leader changes (e.g. if a
/// hero dies and the slot is reassigned).
///
/// HOW TO SET UP:
///   Add this component to the root GameObject of each hero prefab.
///   No extra child objects or Canvas setup needed — the label is created in code.
///
/// The label appears above the existing name / health-bar UI.  Adjust
/// <see cref="offset"/> in the Inspector if it needs to sit higher or lower
/// for your particular prefab layout.
/// </summary>
public class LeaderLabelUI : MonoBehaviour
{
    [Tooltip("World-space offset from the hero's pivot to the label centre.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("Font size of the label (world-space units).")]
    [SerializeField] private float fontSize = 0.35f;

    [Tooltip("Background colour of the crown badge.")]
    [SerializeField] private Color badgeColor = new Color(1f, 0.85f, 0f, 1f);   // gold

    [Tooltip("How often (seconds) the component checks for a leader change.")]
    [SerializeField] private float checkInterval = 0.3f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private GameObject        _labelRoot;
    private TextMeshProUGUI   _text;
    private float             _nextCheck = 0f;
    private bool              _showing   = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildLabel();
    }

    private void OnDestroy()
    {
        if (_labelRoot != null)
            Destroy(_labelRoot);
    }

    private void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + checkInterval;

        bool shouldShow = FormationManager.Instance?.IsLeader(transform) == true;

        if (shouldShow != _showing)
        {
            _showing = shouldShow;
            _labelRoot.SetActive(_showing);
        }
    }

    // ── Label construction ────────────────────────────────────────────────────

    private void BuildLabel()
    {
        // Root GO that follows the hero (child of this transform).
        _labelRoot = new GameObject("_LeaderLabel");
        _labelRoot.transform.SetParent(transform, worldPositionStays: false);
        _labelRoot.transform.localPosition = offset;
        _labelRoot.transform.localRotation = Quaternion.identity;
        _labelRoot.transform.localScale    = Vector3.one;

        // World-space Canvas — same approach as the existing name/health UI.
        var canvas              = _labelRoot.AddComponent<Canvas>();
        canvas.renderMode       = RenderMode.WorldSpace;
        canvas.sortingLayerName = "UI";
        canvas.sortingOrder     = 20;   // above health bar

        var rt        = _labelRoot.GetComponent<RectTransform>();
        rt.sizeDelta  = new Vector2(1.5f, 0.4f);

        // Optional background image for contrast.
        var bg           = _labelRoot.AddComponent<Image>();
        bg.color         = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = false;

        // Text child.
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(_labelRoot.transform, worldPositionStays: false);
        textGO.transform.localPosition = Vector3.zero;
        textGO.transform.localRotation = Quaternion.identity;
        textGO.transform.localScale    = Vector3.one;

        var textRT       = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        _text               = textGO.AddComponent<TextMeshProUGUI>();
        _text.text          = "Leader";
        _text.fontSize      = fontSize;
        _text.color         = badgeColor;
        _text.fontStyle     = FontStyles.Bold;
        _text.alignment     = TextAlignmentOptions.Center;
        _text.raycastTarget = false;

        // Start hidden; Update() will show it on the first check tick.
        _labelRoot.SetActive(false);
    }
}
