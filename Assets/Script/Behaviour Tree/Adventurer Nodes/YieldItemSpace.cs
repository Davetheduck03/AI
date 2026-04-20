using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ACTION: Moves the hero away from a WorldItem drop they cannot use so that
/// party members who CAN use it have clear, unobstructed access.
///
/// PROBLEM THIS SOLVES
/// ───────────────────
/// When enemies die or a chest overflows, multiple heroes converge on the drops.
/// A hero who doesn't need a particular item may park directly on top of it —
/// their collider + SeparationBehavior forces block the path of the hero who
/// does need it, causing them to spin or stop short without ever picking it up.
///
/// HOW IT WORKS
/// ────────────
/// On each BT tick, the hero scans WorldItemRegistry for items within
/// <see cref="YieldCheckRange"/> that:
///   a) are not the hero's own current pickup target (bb["itemTarget"])
///   b) are NOT a personal upgrade (EvaluateNearbyItems already claimed those)
///   c) ARE a class-compatible upgrade for at least one other living party member
///
/// If such an item is found:
///   • Followers step toward the party leader — they clear the drop zone and
///     re-group naturally.
///   • The leader steps directly away from the item — keeps the approach
///     corridor open for followers converging on it.
///
/// Returns Running while still within <see cref="ClearRange"/>;
/// returns Failure once clear (or when the item disappears / no one needs it).
///
/// PRIORITY: insert after the world-item pickup sequence (so the hero collects
/// its own upgrades first) and before FollowLeader (so it yields before
/// re-grouping with the party).
/// </summary>
public class YieldItemSpace : Node
{
    // Within this distance the hero is considered "on top of" the item.
    private const float YieldCheckRange = 1.5f;

    // The hero moves until at least this far from the item before stopping.
    private const float ClearRange = 3.0f;

    // Throttle: WorldItemRegistry iteration + FindGameObjectsWithTag every tick
    // is wasteful.  0.3 s is fast enough to react before a teammate arrives.
    private const float ScanInterval    = 0.3f;
    private float       _nextScanTime   = float.MinValue;
    private WorldItem   _cachedBlocking = null;   // item we're currently yielding for

    // Safety timeout — if something prevents the hero from clearing (e.g. a wall
    // directly behind them), give up after this many seconds rather than spinning.
    private const float MaxYieldSeconds = 2.5f;
    private float       _yieldStart     = float.MinValue;
    private bool        _yielding       = false;

    private GameObject _yieldGO;

    public YieldItemSpace(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        // ── Timeout guard ─────────────────────────────────────────────────────
        if (_yielding && Time.time - _yieldStart > MaxYieldSeconds)
        {
            ResetState();
            return NodeState.Failure;
        }

        // ── Throttled scan ────────────────────────────────────────────────────
        if (Time.time >= _nextScanTime)
        {
            _nextScanTime  = Time.time + ScanInterval;
            _cachedBlocking = FindBlockingItem(self);
        }

        if (_cachedBlocking == null)
        {
            ResetState();
            return NodeState.Failure;
        }

        float dist = Vector3.Distance(self.position, _cachedBlocking.transform.position);

        // Item gone or hero already clear.
        if (_cachedBlocking.gameObject == null || dist >= ClearRange)
        {
            ResetState();
            return NodeState.Failure;
        }

        // ── Start or continue yielding ────────────────────────────────────────
        if (!_yielding)
        {
            _yielding   = true;
            _yieldStart = Time.time;
            Debug.Log($"[YieldItemSpace] {self.name}: yielding space for " +
                      $"{_cachedBlocking.item?.itemName}.");
        }

        Vector3 moveTarget = ComputeYieldPosition(self, _cachedBlocking.transform.position);

        if (_yieldGO == null)
            _yieldGO = new GameObject("_YieldItemSpace")
                       { hideFlags = HideFlags.HideAndDontSave };

        _yieldGO.transform.position = moveTarget;
        self.GetComponent<MovementComponent>()?.OnTriggerMove(self, _yieldGO.transform, 1f);

        return NodeState.Running;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the nearest WorldItem the hero is blocking access to, or null.
    /// </summary>
    private WorldItem FindBlockingItem(Transform self)
    {
        // My own current pickup target — never yield for something I'm collecting.
        Transform myItemTarget = bb.Get<Transform>("itemTarget");

        // Snapshot living non-self party members + their equipment.
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        var others = new List<(Transform t, EquipmentComponent eq)>();

        foreach (GameObject p in players)
        {
            if (p == null || !p.activeInHierarchy || p.transform == self) continue;
            var hp = p.GetComponent<HealthComponent>();
            if (hp != null && hp.currentHealth <= 0) continue;
            var eq = p.GetComponent<EquipmentComponent>();
            if (eq == null) continue;
            others.Add((p.transform, eq));
        }

        if (others.Count == 0) return null;

        // This hero's own equipment — don't yield for items we can use ourselves
        // (EvaluateNearbyItems would have claimed them; this is a safety guard).
        EquipmentComponent selfEq = self.GetComponent<EquipmentComponent>();

        WorldItem nearest     = null;
        float     nearestDist = YieldCheckRange;

        foreach (WorldItem wi in WorldItemRegistry.All)
        {
            if (wi == null || wi.item == null) continue;
            if (wi.gameObject == null)         continue;

            // Don't yield for our own claimed target.
            if (myItemTarget != null && wi.transform == myItemTarget) continue;

            float dist = Vector3.Distance(self.position, wi.transform.position);
            if (dist >= nearestDist) continue;

            // Guard: skip items that are an upgrade for THIS hero — they should
            // be picked up by EvaluateNearbyItems, not yielded.
            if (selfEq != null && IsUpgradeFor(wi.item, selfEq, self)) continue;

            // Only yield if at least one other party member can use it.
            bool someoneElseNeedsIt = false;
            foreach (var (t, eq) in others)
                if (IsUpgradeFor(wi.item, eq, t)) { someoneElseNeedsIt = true; break; }

            if (!someoneElseNeedsIt) continue;

            nearestDist = dist;
            nearest     = wi;
        }

        return nearest;
    }

    /// <summary>
    /// Compute where the hero should move.
    /// Followers step toward the leader (natural re-grouping while clearing the
    /// drop zone).  The leader steps directly away from the item so the approach
    /// corridor stays open.
    /// </summary>
    private Vector3 ComputeYieldPosition(Transform self, Vector3 itemPos)
    {
        FormationManager fm = FormationManager.Instance;

        if (fm != null && !fm.IsLeader(self))
        {
            Transform leader = fm.GetLeader();
            if (leader != null)
            {
                Vector3 toLeader = (leader.position - self.position).normalized;
                if (toLeader.sqrMagnitude > 0.001f)
                    return self.position + toLeader * ClearRange;
            }
        }

        // Leader or no formation: step directly away from the item.
        Vector3 away = (self.position - itemPos).normalized;
        if (away.sqrMagnitude < 0.001f) away = Vector3.right;
        return self.position + away * ClearRange;
    }

    private void ResetState()
    {
        _yielding       = false;
        _cachedBlocking = null;
        // Note: _yieldGO is reused — no need to destroy it between calls.
    }

    // ── Upgrade helpers (mirrors EvaluateNearbyItems / WaitForPartyUpgrades) ──

    private static bool IsUpgradeFor(ItemSO item, EquipmentComponent equipment,
                                     Transform hero)
    {
        if (item is WeaponSO weapon)
        {
            var unit     = hero.GetComponent<BaseUnit>();
            var heroSO   = unit?.unitData as HeroSO;
            var advClass = heroSO?.adventurerClass;
            if (advClass != null && !advClass.CanEquipWeapon(weapon)) return false;
        }

        return item.GetScore() > GetCurrentSlotScore(equipment, item);
    }

    private static float GetCurrentSlotScore(EquipmentComponent eq, ItemSO candidate)
    {
        if (candidate is WeaponSO)    return eq.equippedWeapon?.GetScore() ?? 0f;
        if (candidate is HeadArmorSO) return eq.equippedHead?.GetScore()   ?? 0f;
        if (candidate is BodyArmorSO) return eq.equippedBody?.GetScore()   ?? 0f;
        return 0f;
    }
}
