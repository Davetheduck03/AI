using UnityEngine;

/// <summary>
/// Hides an enemy's renderers and UI canvases while it stands on an unrevealed
/// (fogged) tile, and restores them the moment its tile is revealed by a hero.
///
/// HOW TO USE:
///   This component is added automatically to every enemy at spawn via BaseEnemy.
///   No manual setup is required.
///
/// HOW IT WORKS:
///   The FogOfWarManager tracks which tiles have been permanently revealed.
///   Enemies whose current tile is still fogged have all of their SpriteRenderers
///   and world-space Canvases (health bars, name labels, etc.) disabled.
///   A lightweight poll runs every CheckInterval seconds rather than every frame.
///
/// IMPORTANT:
///   Because the fog in this game is permanent exploration (revealed tiles stay
///   revealed forever), an enemy in a previously-explored area is ALWAYS visible
///   regardless of whether a hero can currently see it.  Only enemies in
///   completely unexplored areas are hidden.
/// </summary>
[DisallowMultipleComponent]
public class EnemyFogVisibility : MonoBehaviour
{
    // How often (seconds) to re-check fog status.
    // 0.12 s gives ~8 checks/sec — responsive without running every frame.
    private const float CheckInterval = 0.12f;

    private FogOfWarManager  _fogManager;
    private SpriteRenderer[] _renderers;
    private Canvas[]         _canvases;

    private bool  _visible      = true;   // current state (avoids redundant SetActive calls)
    private float _nextCheckAt  = 0f;

    private void Awake()
    {
        _fogManager = Object.FindAnyObjectByType<FogOfWarManager>();

        // Collect renderers and canvases including inactive children so everything
        // is captured even if the prefab has disabled UI objects.
        _renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        _canvases  = GetComponentsInChildren<Canvas>(includeInactive: true);

        // Start hidden — enemies spawn in unexplored rooms.
        // The first Update tick will immediately reveal them if their tile is clear.
        ApplyVisibility(false);
    }

    private void Update()
    {
        if (Time.time < _nextCheckAt) return;
        _nextCheckAt = Time.time + CheckInterval;

        bool shouldShow = _fogManager == null || _fogManager.IsRevealed(transform.position);

        if (shouldShow != _visible)
            ApplyVisibility(shouldShow);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplyVisibility(bool show)
    {
        _visible = show;

        foreach (var r in _renderers)
            if (r != null) r.enabled = show;

        foreach (var c in _canvases)
            if (c != null) c.enabled = show;
    }
}
