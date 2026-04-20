 using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Camera controller with two modes:
///   LOCK-ON  — smoothly follows a selected hero. Tab cycles between registered heroes.
///   FREE ROAM — WASD / arrow keys pan; scroll wheel zooms; click+drag also pans.
///
/// Heroes register themselves automatically via BaseHero (OnEnable/OnDisable).
/// Press F (or the configured key) to toggle between modes.
/// </summary>
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    // ─── Inspector ──────────────────────────────────────────────────────────

    [Header("Mode")]
    [Tooltip("Key to toggle between Lock-On and Free Roam.")]
    [SerializeField] private KeyCode toggleModeKey = KeyCode.F;
    [Tooltip("Key to cycle to the next registered hero while in Lock-On mode.")]
    [SerializeField] private KeyCode nextHeroKey = KeyCode.Tab;

    [Header("Lock-On Settings")]
    [Tooltip("How quickly the camera moves to the hero position (higher = snappier).")]
    [SerializeField] private float followSpeed = 6f;
    [Tooltip("Fixed Z depth of the camera.")]
    [SerializeField] private float cameraZ = -10f;

    [Header("Free Roam Settings")]
    [SerializeField] private float panSpeed = 12f;
    [Tooltip("Pan speed multiplier when holding Shift.")]
    [SerializeField] private float shiftMultiplier = 2.5f;
    [SerializeField] private float dragPanSpeed = 0.08f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 20f;

    // ─── State ─────────────────────────────────────────────────────────────

    public enum CameraMode { LockOn, FreeRoam }
    public CameraMode Mode { get; private set; } = CameraMode.LockOn;

    private readonly List<BaseHero> _heroes = new List<BaseHero>();
    private int _heroIndex = 0;

    private Camera _cam;
    private Vector3 _dragOrigin;
    private bool _dragging = false;

    // ─── Hero Registry ─────────────────────────────────────────────────────

    public static void RegisterHero(BaseHero hero)
    {
        if (Instance == null) return;
        if (!Instance._heroes.Contains(hero))
        {
            Instance._heroes.Add(hero);
            // Auto-lock on to first hero that registers
            if (Instance._heroes.Count == 1)
                Instance._heroIndex = 0;
            Debug.Log($"[CameraController] Registered hero: {hero.name} ({Instance._heroes.Count} total)");
        }
    }

    public static void UnregisterHero(BaseHero hero)
    {
        if (Instance == null) return;
        int idx = Instance._heroes.IndexOf(hero);
        Instance._heroes.Remove(hero);

        // Keep index in bounds
        if (Instance._heroes.Count > 0)
            Instance._heroIndex = Mathf.Clamp(Instance._heroIndex, 0, Instance._heroes.Count - 1);
        else
            Instance._heroIndex = 0;

        Debug.Log($"[CameraController] Unregistered hero: {hero.name} ({Instance._heroes.Count} remaining)");
    }

    // ─── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    private void Update()
    {
        HandleModeToggle();
        HandleZoom();

        if (Mode == CameraMode.LockOn)
            UpdateLockOn();
        else
            UpdateFreeRoam();
    }

    // ─── Mode Toggle ────────────────────────────────────────────────────────

    private void HandleModeToggle()
    {
        if (Input.GetKeyDown(toggleModeKey))
        {
            Mode = (Mode == CameraMode.LockOn) ? CameraMode.FreeRoam : CameraMode.LockOn;
            Debug.Log($"[CameraController] Mode → {Mode}");
        }
    }

    // ─── Lock-On ────────────────────────────────────────────────────────────

    private void UpdateLockOn()
    {
        // Cycle heroes
        if (Input.GetKeyDown(nextHeroKey) && _heroes.Count > 1)
        {
            _heroIndex = (_heroIndex + 1) % _heroes.Count;
            Debug.Log($"[CameraController] Locked onto: {_heroes[_heroIndex].name}");
        }

        if (_heroes.Count == 0) return;

        // Clamp in case a hero was removed
        _heroIndex = Mathf.Clamp(_heroIndex, 0, _heroes.Count - 1);
        BaseHero target = _heroes[_heroIndex];
        if (target == null) return;

        Vector3 desired = target.transform.position;
        desired.z = cameraZ;
        transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
    }

    // ─── Free Roam ──────────────────────────────────────────────────────────

    private void UpdateFreeRoam()
    {
        float speed = panSpeed * (Input.GetKey(KeyCode.LeftShift) ? shiftMultiplier : 1f) * Time.deltaTime;

        // Keyboard pan
        float h = Input.GetAxisRaw("Horizontal");   // A/D or ←/→
        float v = Input.GetAxisRaw("Vertical");     // W/S or ↑/↓
        Vector3 move = new Vector3(h, v, 0f).normalized * speed;
        transform.position += move;

        // Middle-mouse drag pan
        if (Input.GetMouseButtonDown(2))
        {
            _dragOrigin = _cam.ScreenToWorldPoint(Input.mousePosition);
            _dragging = true;
        }
        if (Input.GetMouseButtonUp(2)) _dragging = false;
        if (_dragging)
        {
            Vector3 diff = _dragOrigin - _cam.ScreenToWorldPoint(Input.mousePosition);
            transform.position += diff;
        }

        // Snap Z
        Vector3 pos = transform.position;
        pos.z = cameraZ;
        transform.position = pos;
    }

    // ─── Zoom ───────────────────────────────────────────────────────────────

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        if (_cam.orthographic)
        {
            _cam.orthographicSize = Mathf.Clamp(
                _cam.orthographicSize - scroll * zoomSpeed * 10f,
                minZoom, maxZoom);
        }
        else
        {
            // Perspective: move camera along Z
            Vector3 pos = transform.position;
            pos.z = Mathf.Clamp(pos.z + scroll * zoomSpeed * 10f, -maxZoom * 2f, -minZoom);
            transform.position = pos;
        }
    }

    // ─── Public Helpers ─────────────────────────────────────────────────────

    /// <summary>Jump the camera immediately to a hero by index (no lerp).</summary>
    public void SnapToHero(int index)
    {
        if (index < 0 || index >= _heroes.Count) return;
        _heroIndex = index;
        Mode = CameraMode.LockOn;
        Vector3 pos = _heroes[index].transform.position;
        pos.z = cameraZ;
        transform.position = pos;
    }

    /// <summary>Lock onto a specific hero instance.</summary>
    public void LockOnTo(BaseHero hero)
    {
        int idx = _heroes.IndexOf(hero);
        if (idx >= 0) { _heroIndex = idx; Mode = CameraMode.LockOn; }
    }

    public int HeroCount => _heroes.Count;
    public BaseHero GetHero(int index) => (index >= 0 && index < _heroes.Count) ? _heroes[index] : null;
    public BaseHero CurrentHero => _heroes.Count > 0 ? _heroes[_heroIndex] : null;

#if UNITY_EDITOR
    private void OnGUI()
    {
        // Only show the debug HUD once the game is actually running (not during party select).
        if (!(GameManager.Instance?.GetCurrentState() is RoundState_InGame))
            return;

        // Tiny HUD overlay in the top-left for debugging
        GUI.color = Color.black;
        string heroName = CurrentHero != null ? CurrentHero.name : "none";
        string label    = Mode == CameraMode.LockOn
            ? $"[{toggleModeKey}] FREE ROAM   [{nextHeroKey}] Next Hero: {heroName}"
            : $"[{toggleModeKey}] LOCK ON   WASD=pan  Scroll=zoom  MMB=drag";
        GUI.Label(new Rect(11, 11, 500, 22), label);
        GUI.color = Mode == CameraMode.LockOn ? Color.cyan : Color.yellow;
        GUI.Label(new Rect(10, 10, 500, 22), label);
    }
#endif
}
