using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages party formation using a designated LEADER model:
///
///   • The highest-priority hero (melee / highest max-HP) becomes the LEADER.
///     The leader explores normally via its own FindFogCluster AI.
///
///   • Every other hero is a FOLLOWER.  Followers use the <see cref="FollowLeader"/>
///     BT node to keep themselves in a slot behind / beside the leader instead of
///     scattering to independent fog clusters.
///
/// Slot layout (relative to the leader, orientated along the leader's travel dir):
///
///           [ LEADER 0 ]            ← front  (melee, highest HP)
///       [ 1 ]                       ← beside (melee partner)
///   [ 2 ]       [ 3 ]               ← back   (ranged)
///
/// Setup: add this component to any persistent scene object that also has
/// DungeonSpawner (or any other always-alive GameObject).
/// </summary>
public class FormationManager : MonoBehaviour
{
    public static FormationManager Instance { get; private set; }

    // ── Follower offset tables (forward, right) relative to the leader ────────
    // Positive forward = in front of leader.  Negative = behind.
    // "Forward" is the leader's last known movement direction.

    private static readonly Vector2[] Follow1 = {
        new Vector2(0f, 0f),
    };
    private static readonly Vector2[] Follow2 = {
        new Vector2(  0f,   0f),   // slot 0: leader
        new Vector2(-1.5f,  0f),   // slot 1: directly behind leader
    };
    private static readonly Vector2[] Follow3 = {
        new Vector2(  0f,   0f),   // slot 0: leader
        new Vector2(-1.5f, -0.7f), // slot 1: behind-left
        new Vector2(-1.5f,  0.7f), // slot 2: behind-right
    };
    private static readonly Vector2[] Follow4 = {
        new Vector2(  0f,   0f),   // slot 0: leader
        new Vector2(-0.2f,  0.9f), // slot 1: beside-right (melee partner)
        new Vector2(-2.0f, -0.6f), // slot 2: back-left (ranged)
        new Vector2(-2.0f,  0.6f), // slot 3: back-right (ranged)
    };

    // ── State ─────────────────────────────────────────────────────────────────

    private List<Transform>            _heroes  = new List<Transform>();
    private Dictionary<Transform, int> _slotMap = new Dictionary<Transform, int>();

    // Cached leader movement direction — updated every frame from the leader's
    // position delta so followers know which way "forward" is.
    private Vector3 _leaderForward  = Vector3.right;
    private Vector3 _lastLeaderPos  = Vector3.zero;
    private bool    _leaderPosValid = false;

    private float _nextRefresh = 0f;
    private const float RefreshInterval = 3f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        DungeonSpawner.OnPartySpawned += OnPartySpawned;
        HealthComponent.OnDeath       += OnUnitDied;
    }

    private void OnDestroy()
    {
        DungeonSpawner.OnPartySpawned -= OnPartySpawned;
        HealthComponent.OnDeath       -= OnUnitDied;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Refresh slots periodically (picks up weapon swaps, etc.)
        if (Time.time >= _nextRefresh)
        {
            _nextRefresh = Time.time + RefreshInterval;
            RefreshSlots();
        }

        // Track the leader's movement direction so followers orient correctly.
        Transform leader = GetLeader();
        if (leader != null)
        {
            if (_leaderPosValid)
            {
                Vector3 delta = leader.position - _lastLeaderPos;
                if (delta.sqrMagnitude > 0.0001f)
                    _leaderForward = delta.normalized;
            }
            _lastLeaderPos  = leader.position;
            _leaderPosValid = true;
        }
        else
        {
            _leaderPosValid = false;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnPartySpawned(List<GameObject> heroes)
    {
        _heroes = heroes.Where(h => h != null).Select(h => h.transform).ToList();
        _leaderPosValid = false;
        RefreshSlots();
        Debug.Log($"[FormationManager] Party of {_heroes.Count} registered.");
    }

    private void OnUnitDied(HealthComponent hc)
    {
        if (_heroes.Remove(hc.transform))
        {
            _leaderPosValid = false;
            RefreshSlots();
            Debug.Log($"[FormationManager] {hc.name} died — slots reassigned.");
        }
    }

    // ── Slot assignment ───────────────────────────────────────────────────────

    public void RefreshSlots()
    {
        _heroes.RemoveAll(h => h == null || h.gameObject == null);

        // Melee (non-bow) heroes go to the front; bow heroes go to the back.
        // Within each group the highest max-HP hero gets the lower slot index.
        var sorted = _heroes
            .OrderBy(h  => RolePriority(h))
            .ThenByDescending(h => MaxHealth(h))
            .ToList();

        _slotMap.Clear();
        for (int i = 0; i < sorted.Count; i++)
            _slotMap[sorted[i]] = i;

        string slotSummary = string.Join(", ",
            sorted.Select((h, i) => $"[{i}]{h.name}({(RolePriority(h) == 0 ? "melee" : "bow")})"));
        Debug.Log($"[FormationManager] Slots: {slotSummary}");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True if <paramref name="hero"/> occupies slot 0 (the leader).</summary>
    public bool IsLeader(Transform hero) =>
        _slotMap.TryGetValue(hero, out int slot) && slot == 0;

    /// <summary>Returns the current leader Transform, or null if none.</summary>
    public Transform GetLeader()
    {
        foreach (var kv in _slotMap)
            if (kv.Value == 0 && kv.Key != null && kv.Key.gameObject != null)
                return kv.Key;
        return null;
    }

    /// <summary>
    /// Returns the world-space position <paramref name="follower"/> should walk to
    /// in order to maintain its formation slot behind the leader.
    /// </summary>
    public Vector3 GetFollowPosition(Transform follower)
    {
        Transform leader = GetLeader();
        if (leader == null || !_slotMap.TryGetValue(follower, out int slot))
            return follower != null ? follower.position : Vector3.zero;

        Vector2[] table = _heroes.Count switch
        {
            1 => Follow1,
            2 => Follow2,
            3 => Follow3,
            _ => Follow4,
        };

        slot = Mathf.Clamp(slot, 0, table.Length - 1);
        Vector2 off = table[slot];

        Vector3 fwd   = _leaderForward;
        Vector3 right = new Vector3(-fwd.y, fwd.x, 0f);

        return leader.position + fwd * off.x + right * off.y;
    }

    /// <summary>
    /// Corridor fallback position: heroes line up directly behind the leader with no
    /// lateral offset. Used by <see cref="FollowLeader"/> when the normal slot lands
    /// inside a wall (e.g. in a narrow corridor).
    /// Slot 1 → 1.5 units back, slot 2 → 3.0, slot 3 → 4.5.
    /// </summary>
    public Vector3 GetFallbackPosition(Transform follower)
    {
        Transform leader = GetLeader();
        if (leader == null || !_slotMap.TryGetValue(follower, out int slot))
            return follower != null ? follower.position : Vector3.zero;

        return leader.position + (-_leaderForward) * (slot * 1.5f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>0 = melee/front, 1 = ranged/back (Bow or Staff).</summary>
    private static int RolePriority(Transform hero)
    {
        var eq = hero.GetComponent<EquipmentComponent>();
        var wt = eq?.equippedWeapon?.weaponType;
        return (wt == WeaponType.Bow || wt == WeaponType.Staff) ? 1 : 0;
    }

    private static float MaxHealth(Transform hero) =>
        hero.GetComponent<HealthComponent>()?.maxHealth ?? 0f;
}
