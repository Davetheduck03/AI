using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that accumulates damage-dealt totals per hero for the current run.
///
/// Data flow
/// ─────────
///   HealthComponent.OnDamageDealt  → records (dealer, amount) into _totals
///   DungeonSpawner.OnPartySpawned  → resets totals and registers the current hero list
///   DamageTrackerPanelUI           → polls GetTotal(hero) each Update()
///
/// Attach this MonoBehaviour to any persistent HUD GameObject.
/// </summary>
public class DamageTracker : MonoBehaviour
{
    public static DamageTracker Instance { get; private set; }

    // Per-hero accumulated damage (key = hero GameObject).
    private readonly Dictionary<GameObject, float> _totals = new Dictionary<GameObject, float>();

    // The heroes registered for the current run (ordered by spawn index).
    private List<GameObject> _heroes = new List<GameObject>();

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        HealthComponent.OnDamageDealt  += OnDamageDealt;
        DungeonSpawner.OnPartySpawned  += OnPartySpawned;
    }

    private void OnDisable()
    {
        HealthComponent.OnDamageDealt  -= OnDamageDealt;
        DungeonSpawner.OnPartySpawned  -= OnPartySpawned;
    }

    // ── Event handlers ─────────────────────────────────────────────────────────

    private void OnDamageDealt(GameObject dealer, float amount)
    {
        if (dealer == null) return;
        if (!_totals.ContainsKey(dealer))
            _totals[dealer] = 0f;
        _totals[dealer] += amount;
    }

    private void OnPartySpawned(List<GameObject> heroes)
    {
        _totals.Clear();
        _heroes = heroes;

        // Pre-populate so every hero always has an entry.
        foreach (var hero in heroes)
            if (hero != null) _totals[hero] = 0f;
    }

    // ── Query API ──────────────────────────────────────────────────────────────

    /// <summary>Returns the total damage dealt by the given hero this run.</summary>
    public float GetTotal(GameObject hero)
    {
        if (hero == null) return 0f;
        return _totals.TryGetValue(hero, out float val) ? val : 0f;
    }

    /// <summary>The ordered hero list registered at the start of the current run.</summary>
    public IReadOnlyList<GameObject> Heroes => _heroes;
}
