using UnityEngine;

/// <summary>
/// Full-screen Game Over overlay shown when the entire party is wiped.
///
/// USAGE
///   GameOverScreen.Show(floorsCompleted, floorDiedOn) from RoundState_Lose.
///   The user dismisses it with "Play Again" which triggers the actual reset.
///
/// SELF-CREATING
///   The first call to Show() instantiates a persistent GameObject carrying
///   this component — no prefab or scene setup required.
///
/// LAYOUT  (drawn with OnGUI, no Canvas dependency)
///   ┌────────────────────────────────────┐
///   │         PARTY WIPED                │
///   │                                    │
///   │   Reached floor  4                 │
///   │   Floors cleared 3                 │
///   │                                    │
///   │        [ Play Again ]              │
///   └────────────────────────────────────┘
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    public static GameOverScreen Instance { get; private set; }

    // ── State set by Show() ───────────────────────────────────────────────────
    private bool _visible         = false;
    private int  _floorsCompleted = 0;
    private int  _floorDiedOn     = 1;

    // ── Interaction cooldown — prevents accidental double-clicks ─────────────
    private float _showTime = 0f;
    private const float ClickCooldown = 0.6f;

    // ── Sizing constants ──────────────────────────────────────────────────────
    private const float PanelW = 480f;
    private const float PanelH = 320f;

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

    /// <summary>
    /// Creates the singleton (if not yet created) and shows the overlay.
    /// Called from RoundState_Lose before any cleanup so the data is captured.
    /// </summary>
    public static void Show(int floorsCompleted, int floorDiedOn)
    {
        if (Instance == null)
        {
            var go = new GameObject("[GameOverScreen]");
            go.AddComponent<GameOverScreen>();
        }

        Instance._floorsCompleted = floorsCompleted;
        Instance._floorDiedOn     = floorDiedOn;
        Instance._visible         = true;
        Instance._showTime        = Time.unscaledTime;
    }

    public static void Hide()
    {
        if (Instance != null)
            Instance._visible = false;
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_visible) return;

        float sw = Screen.width;
        float sh = Screen.height;

        // ── Dark vignette over the game world ─────────────────────────────────
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // ── Centred panel ─────────────────────────────────────────────────────
        float px = (sw - PanelW) * 0.5f;
        float py = (sh - PanelH) * 0.5f;

        // Panel background
        GUI.color = new Color(0.10f, 0.08f, 0.12f, 0.96f);
        GUI.DrawTexture(new Rect(px, py, PanelW, PanelH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Thin border
        DrawBorder(px, py, PanelW, PanelH, new Color(0.55f, 0.25f, 0.25f, 1f), 2f);

        GUILayout.BeginArea(new Rect(px + 20f, py + 20f, PanelW - 40f, PanelH - 40f));

        // ── Title ─────────────────────────────────────────────────────────────
        GUILayout.Space(12f);
        GUILayout.Label("PARTY WIPED", TitleStyle());
        GUILayout.Space(24f);

        // ── Stats ─────────────────────────────────────────────────────────────
        GUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Reached floor",   LabelStyle(), GUILayout.Width(200f));
        GUILayout.Label(_floorDiedOn.ToString(), ValueStyle());
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Floors cleared",  LabelStyle(), GUILayout.Width(200f));
        string clearedText = _floorsCompleted > 0
            ? _floorsCompleted.ToString()
            : "none";
        GUILayout.Label(clearedText, ValueStyle());
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.Space(40f);

        // ── Play Again button ─────────────────────────────────────────────────
        bool cooldownOver = Time.unscaledTime - _showTime >= ClickCooldown;

        GUI.enabled = cooldownOver;
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Play Again", ButtonStyle(), GUILayout.Width(180f), GUILayout.Height(44f)))
            OnPlayAgain();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUILayout.EndArea();
    }

    // ── Button handler ────────────────────────────────────────────────────────

    private void OnPlayAgain()
    {
        _visible = false;

        // Reset floor tracking so the next run starts at floor 1
        RunProgressionManager.Instance?.ResetProgress();

        // Now do the actual game cleanup and go back to party selection
        DungeonSpawner.Instance?.CleanupAll();
        PartyData.Instance?.ClearParty();
        GameManager.Instance?.SwitchState(GameManager.Instance.PartySelect);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void DrawBorder(float x, float y, float w, float h, Color c, float thickness)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(x,             y,             w,         thickness), Texture2D.whiteTexture); // top
        GUI.DrawTexture(new Rect(x,             y + h - thickness, w,    thickness), Texture2D.whiteTexture); // bottom
        GUI.DrawTexture(new Rect(x,             y,             thickness, h        ), Texture2D.whiteTexture); // left
        GUI.DrawTexture(new Rect(x + w - thickness, y,        thickness, h        ), Texture2D.whiteTexture); // right
        GUI.color = Color.white;
    }

    // ── GUI styles ────────────────────────────────────────────────────────────

    private static GUIStyle TitleStyle()
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.95f, 0.35f, 0.35f) },
        };
        return s;
    }

    private static GUIStyle LabelStyle()
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 20,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = new Color(0.75f, 0.75f, 0.75f) },
        };
        return s;
    }

    private static GUIStyle ValueStyle()
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = new Color(1.0f, 0.85f, 0.40f) },  // gold
        };
        return s;
    }

    private static GUIStyle ButtonStyle()
    {
        var s = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 18,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white },
            hover     = { textColor = new Color(1f, 0.9f, 0.5f) },
        };
        return s;
    }
}
