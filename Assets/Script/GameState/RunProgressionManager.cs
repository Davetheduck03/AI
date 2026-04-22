using UnityEngine;

/// <summary>
/// Tracks how many dungeon floors the party has completed in the current session.
/// FloorNumber starts at 1 and increments each time RoundState_Win fires.
///
/// ENEMY SCALING — two independent axes:
///
///   Floor scaling (additive per completed floor):
///     healthMultiplier = 1 + (FloorNumber - 1) × HealthScalePerFloor
///     damageMultiplier = 1 + (FloorNumber - 1) × DamageScalePerFloor
///     Floor 1 = ×1.0.  Floor 2 = ×1.20.  Floor 5 = ×1.80.
///
///   Party-size scaling (additive per hero above 1):
///     healthMultiplier += (partySize - 1) × HealthScalePerHero
///     damageMultiplier += (partySize - 1) × DamageScalePerHero
///     1 hero = baseline.  4 heroes = +3 × scale (enemies must handle more DPS).
///
///   Both factors multiply together:
///     final = floorScale × partyScale   (each component ≥ 1.0)
///
///   DungeonSpawner calls GetHealthMultiplier(partySize) / GetDamageMultiplier(partySize)
///   so the spawner drives the party-count lookup — the manager stays stateless on it.
///
/// Persists across floor transitions (DontDestroyOnLoad). Reset on full Lose cleanup.
/// </summary>
public class RunProgressionManager : MonoBehaviour
{
    public static RunProgressionManager Instance { get; private set; }

    [Header("Scaling Per Floor (additive)")]
    [Tooltip("HP multiplier increase per completed floor.  0.20 = +20 % per floor.")]
    [SerializeField] private float _healthScalePerFloor = 0.20f;

    [Tooltip("Damage multiplier increase per completed floor.  0.15 = +15 % per floor.")]
    [SerializeField] private float _damageScalePerFloor = 0.15f;

    [Header("Scaling Per Additional Hero (additive)")]
    [Tooltip("Extra HP multiplier per hero above 1.  0.15 = +15 % per additional hero.")]
    [SerializeField] private float _healthScalePerHero = 0.15f;

    [Tooltip("Extra damage multiplier per hero above 1.  0.10 = +10 % per additional hero.")]
    [SerializeField] private float _damageScalePerHero = 0.10f;

    /// <summary>Current floor number. 1 = first floor (no floor scaling applied).</summary>
    public int FloorNumber { get; private set; } = 1;

    /// <summary>
    /// Number of floors fully completed (extracted from) in the current run.
    /// 0 on floor 1, increments to FloorNumber-1 on each Win.
    /// Displayed on the Game Over screen.
    /// </summary>
    public int FloorsCompleted => FloorNumber - 1;

    // ── Convenience properties (party-size-agnostic) ──────────────────────────
    /// <summary>Floor-only HP multiplier (party-size factor NOT included).</summary>
    public float HealthMultiplier => 1f + (FloorNumber - 1) * _healthScalePerFloor;

    /// <summary>Floor-only damage multiplier (party-size factor NOT included).</summary>
    public float DamageMultiplier => 1f + (FloorNumber - 1) * _damageScalePerFloor;

    // ── Party-aware multipliers — call these from DungeonSpawner ─────────────

    /// <summary>
    /// Returns the combined HP multiplier for the current floor and given party size.
    /// <paramref name="partySize"/> = number of living heroes at spawn time.
    /// </summary>
    public float GetHealthMultiplier(int partySize)
    {
        float floorFactor = 1f + (FloorNumber - 1) * _healthScalePerFloor;
        float heroFactor  = 1f + Mathf.Max(partySize - 1, 0) * _healthScalePerHero;
        return floorFactor * heroFactor;
    }

    /// <summary>
    /// Returns the combined damage multiplier for the current floor and given party size.
    /// </summary>
    public float GetDamageMultiplier(int partySize)
    {
        float floorFactor = 1f + (FloorNumber - 1) * _damageScalePerFloor;
        float heroFactor  = 1f + Mathf.Max(partySize - 1, 0) * _damageScalePerHero;
        return floorFactor * heroFactor;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Called by RoundState_Win when the party completes a floor.
    /// Increments the floor counter so the next SpawnAll applies stronger scaling.
    /// </summary>
    public void IncrementFloor()
    {
        FloorNumber++;
        Debug.Log($"[RunProgression] Floor advanced to {FloorNumber} — " +
                  $"HP ×{HealthMultiplier:F2}, DMG ×{DamageMultiplier:F2} (floor only; " +
                  $"call GetHealthMultiplier(partySize) for the full value)");
    }

    /// <summary>
    /// Resets to floor 1. Called by RoundState_Lose / GameManager when the party wipes
    /// and the player restarts from the party selection screen.
    /// </summary>
    public void ResetProgress()
    {
        FloorNumber = 1;
        Debug.Log("[RunProgression] Progress reset — back to floor 1.");
    }
}
