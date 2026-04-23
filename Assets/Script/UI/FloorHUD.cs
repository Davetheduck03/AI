using UnityEngine;

/// <summary>
/// Draws a "Floor X" label at the top-centre of the screen during gameplay.
///
/// SELF-CREATING
///   Call FloorHUD.Show() once (e.g. from RoundState_InGame.EnterState) and
///   the singleton GameObject is created automatically — no prefab or scene setup.
///   Alternatively, it can live as a DontDestroyOnLoad object created on first
///   access just by calling FloorHUD.Instance.
///
/// DISPLAY
///   Visible only when _visible = true (between Show() and Hide() calls).
///   Reads RunProgressionManager.Instance.FloorNumber every frame so the label
///   always reflects the current floor without needing explicit refresh calls.
///
/// LAYOUT
///   ┌──────────────────────────────────┐
///   │           Floor  3               │  ← top centre, 10 px margin
///   │  ...rest of game world...        │
///   └──────────────────────────────────┘
/// </summary>
public class FloorHUD : MonoBehaviour
{
    public static FloorHUD Instance { get; private set; }

    private bool _visible = false;

    // ── Sizing ────────────────────────────────────────────────────────────────
    private const float LabelW  = 220f;
    private const float LabelH  = 40f;
    private const float MarginY = 10f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Creates the singleton if needed and makes the label visible.</summary>
    public static void Show()
    {
        EnsureInstance();
        Instance._visible = true;
    }

    /// <summary>Hides the label (e.g. during party selection or game over).</summary>
    public static void Hide()
    {
        if (Instance != null)
            Instance._visible = false;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_visible) return;

        int floor = RunProgressionManager.Instance?.FloorNumber ?? 1;

        float sw = Screen.width;
        float x  = (sw - LabelW) * 0.5f;
        float y  = MarginY;

        // Soft dark backing pill so the text is readable over any background
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(x - 8f, y - 4f, LabelW + 16f, LabelH + 8f),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(x, y, LabelW, LabelH), $"Floor  {floor}", LabelStyle());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("[FloorHUD]");
        go.AddComponent<FloorHUD>();
    }

    private static GUIStyle LabelStyle()
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(1.0f, 0.88f, 0.45f) },  // warm gold
        };
        return s;
    }
}
