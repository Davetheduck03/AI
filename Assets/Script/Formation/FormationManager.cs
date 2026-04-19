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
        new Vector2(-0.9f,  0f),   // slot 1: directly behind leader
    };
    private static readonly Vector2[] Follow3 = {
        new Vector2(  0f,   0f),   // slot 0: leader
        new Vector2(-0.9f,  0.3f), // slot 1: behind-right
        new Vector2(-1.5f, -0.3f), // slot 2: further behind-left
    };
    private static readonly Vector2[] Follow4 = {
        new Vector2(  0f,   0f),   // slot 0: leader
        new Vector2(-0.9f,  0.3f), // slot 1: behind-right
        new Vector2(-1.5f, -0.3f), // slot 2: further behind-left
        new Vector2(-2.0f,  0.3f), // slot 3: furthest behind-right
    };

    // ── State ─────────────────────────────────────────────────────────────────

    private List<Transform>            _heroes  = new List<Transform>();
    private Dictionary<Transform, int> _slotMap = new Dictionary<Transform, int>();

    // Cached leader facing direction.
    // Priority: nearest visible enemy direction > leader movement direction.
    // This keeps followers behind the leader relative to the threat at all times,
    // even when the party is standing still.
    private Vector3 _leaderForward  = Vector3.right;
    private Vector3 _lastLeaderPos  = Vector3.zero;
    private bool    _leaderPosValid = false;

    // Throttle the enemy scan — no need to run FindGameObjectsWithTag every frame.
    private FogOfWarManager _fogManager;
    private float   _nextEnemyDirCheck  = 0f;
    private Vector3 _cachedEnemyDir     = Vector3.zero;   // zero = no visible enemy
    private const float EnemyDirInterval = 0.25f;


    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
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
        // Slots are only assigned on spawn and on death — never on a timer.
        // Periodic refresh was removed to prevent mid-run leadership swaps
        // caused by relic HP bonuses or weapon pickups altering the sort order.

        // Scan for visible enemies periodically (cheaper than every frame).
        if (Time.time >= _nextEnemyDirCheck)
        {
            _nextEnemyDirCheck = Time.time + EnemyDirInterval;
            _cachedEnemyDir    = GetNearestVisibleEnemyDir();
        }

        Transform leader = GetLeader();
        if (leader != null)
        {
            if (_cachedEnemyDir != Vector3.zero)
            {
                // Enemies are visible — face the threat so followers shelter behind the leader.
                _leaderForward = _cachedEnemyDir;
            }
            else if (_leaderPosValid)
            {
                // No visible enemies — track the leader's movement direction as before.
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

    /// <summary>
    /// Returns the normalised direction from the leader toward the nearest revealed
    /// (non-fog) enemy, or Vector3.zero if no enemies are currently visible.
    /// </summary>
    private Vector3 GetNearestVisibleEnemyDir()
    {
        Transform leader = GetLeader();
        if (leader == null) return Vector3.zero;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest    = null;
        float     closestSq  = float.MaxValue;

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;
            if (_fogManager != null && !_fogManager.IsRevealed(enemyObj.transform.position))
                continue;

            // Ignore dead enemies — they should not dictate leader facing direction
            // while a death animation plays before the GameObject is destroyed.
            var hp = enemyObj.GetComponent<HealthComponent>();
            if (hp != null && hp.currentHealth <= 0) continue;

            float sq = (enemyObj.transform.position - leader.position).sqrMagnitude;
            if (sq < closestSq)
            {
                closestSq = sq;
                nearest   = enemyObj.transform;
            }
        }

        if (nearest == null) return Vector3.zero;
        return (nearest.position - leader.position).normalized;
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

        // Remember who the current leader is before we reassign anything.
        // If the leader is still alive they keep slot 0 — we never transfer
        // leadership due to a weapon swap or relic pickup mid-run.
        // Leadership only changes when the leader dies (OnUnitDied removes them
        // from _heroes, so GetLeader returns null and we re-elect normally).
        Transform existingLeader = GetLeader();
        bool keepLeader = existingLeader != null && _heroes.Contains(existingLeader);

        // Sort all heroes by role priority, then by max HP as a tiebreaker.
        var sorted = _heroes
            .OrderBy(h  => RolePriority(h))
            .ThenByDescending(h => MaxHealth(h))
            .ToList();

        _slotMap.Clear();

        if (keepLeader)
        {
            // Pin the existing leader at slot 0, then assign the remaining
            // heroes to slots 1-N in their sorted order.
            _slotMap[existingLeader] = 0;
            int nextSlot = 1;
            foreach (var h in sorted)
            {
                if (h == existingLeader) continue;
                _slotMap[h] = nextSlot++;
            }
        }
        else
        {
            // Fresh start or leader died — elect from scratch.
            for (int i = 0; i < sorted.Count; i++)
                _slotMap[sorted[i]] = i;
        }

        string slotSummary = string.Join(", ",
            sorted.Select(h => $"[{_slotMap[h]}]{h.name}"));
        Debug.Log($"[FormationManager] Slots: {slotSummary}");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>True if <paramref name="hero"/> occupies slot 0 (the leader).</summary>
    public bool IsLeader(Transform hero) =>
        _slotMap.TryGetValue(hero, out int slot) && slot == 0;

    /// <summary>
    /// Returns the formation slot index for <paramref name="hero"/> (0 = leader, highest priority),
    /// or -1 if the hero is not registered.
    /// </summary>
    public int GetSlot(Transform hero) =>
        _slotMap.TryGetValue(hero, out int slot) ? slot : -1;

    /// <summary>The direction the leader is currently considered to be facing.</summary>
    public Vector3 GetLeaderForward() => _leaderForward;

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

        // Use the same longitudinal depth as the normal slot table but with no lateral
        // offset, so the corridor fallback stays consistent with the normal formation depth.
        Vector2[] fallbackTable = _heroes.Count switch
        {
            1 => Follow1,
            2 => Follow2,
            3 => Follow3,
            _ => Follow4,
        };
        slot = Mathf.Clamp(slot, 0, fallbackTable.Length - 1);
        float depth = Mathf.Abs(fallbackTable[slot].x);
        return leader.position + (-_leaderForward) * depth;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Slot ordering priority (lower = closer to front):
    ///   0 — melee (sword, etc.)   → lead the group, absorb hits
    ///   1 — healer (staff + HealerAI) → stay close behind melee so heal range reaches
    ///   2 — mage  (staff, no heal)    → mid-back
    ///   3 — archer (bow)              → furthest back, safe kiting distance
    /// </summary>
    private static int RolePriority(Transform hero)
    {
        var eq = hero.GetComponent<EquipmentComponent>();
        var wt = eq?.equippedWeapon?.weaponType;

        if (wt == WeaponType.Bow) return 3;  // archer — furthest back

        // Staff: healer if it has a HealerAI component, mage otherwise.
        if (wt == WeaponType.Staff)
            return hero.GetComponent<HealerAI>() != null ? 1 : 2;

        // Everything else (Sword, LongSword, Dagger, …) → melee front-liner.
        return 0;
    }

    private static float MaxHealth(Transform hero)
    {
        var hc = hero.GetComponent<HealthComponent>();
        return hc != null ? hc.maxHealth : 0f;
    }
}