using UnityEngine;

/// <summary>
/// ACTION (leader only): Ensures the party collects equipment upgrades before
/// moving on by actively steering the leader toward any upgrade drop, then
/// holding position once close enough for followers to pick it up.
///
/// FLOW
/// ────
/// 1. Scans WorldItemRegistry for revealed, walkable items that are genuine
///    upgrades for at least one living non-leader party member.
/// 2. If found: leader MOVES toward the nearest upgrade (so followers who
///    converge via FollowLeader end up within EvaluateNearbyItems pickup range).
///    Once within <see cref="GatherRadius"/> the leader stops and holds position.
/// 3. Returns Running until no upgrade remains; then Failure (falls through
///    to Explore).
///
/// WHY THE LEADER MOVES (not just stops)
/// ──────────────────────────────────────
/// EvaluateNearbyItems scans 16 u from the follower.  IsLeaderOrNearLeader lets
/// followers diverge up to 7 u from the leader.  If the leader stops >9 u from
/// an item, a follower standing next to the leader is >16 u away and their Items
/// sequence never fires.  By driving the leader to within GatherRadius (8 u) of
/// the drop, followers who arrive close to the leader are guaranteed to see the
/// item within their 16 u scan radius.
///
/// Non-leaders always return Failure immediately — safe to add to every AI class.
/// </summary>
public class WaitForPartyUpgrades : Node
{
    // Leader stops within this distance of the nearest upgrade so followers
    // who arrive nearby can pick it up with EvaluateNearbyItems (16 u range).
    // 8 u  +  7 u (follower–leader gap via IsLeaderOrNearLeader)  =  15 u < 16 u ✓
    private const float GatherRadius = 8f;

    // How wide to search for follower upgrades relative to the leader.
    // Keep <= 20 u so we don't wait for items two rooms away that followers
    // would have to cross uncleared fog to reach.
    private readonly float _searchRange;

    // Throttle: WorldItemRegistry iteration is O(n).  0.5 s is fast enough to
    // react within one pickup cycle.
    private const float ScanInterval = 0.5f;
    private float       _nextScanTime = float.MinValue;

    // Cached position of the nearest follower upgrade (null = none).
    private Vector3? _upgradeTarget = null;

    // Safety valve: if the party hasn't collected the upgrade after this many
    // seconds, give up and resume exploration (prevents permanent deadlock when
    // an item is unreachable or the only hero who needs it has died).
    private const float MaxWaitSeconds = 12f;
    private float       _waitStartTime  = float.MinValue;
    private bool        _waiting        = false;

    // Helper GO used as the movement target when steering toward the upgrade.
    private GameObject _gatherGO;

    private FogOfWarManager _fogManager;

    public WaitForPartyUpgrades(Blackboard bb, float searchRange = 20f) : base(bb)
    {
        _searchRange = searchRange;
        _fogManager  = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        FormationManager fm = FormationManager.Instance;
        if (fm == null || !fm.IsLeader(self)) return NodeState.Failure;

        // Throttle the item scan.
        if (Time.time >= _nextScanTime)
        {
            _nextScanTime  = Time.time + ScanInterval;
            _upgradeTarget = FindNearestFollowerUpgrade(self);
        }

        if (_upgradeTarget.HasValue)
        {
            // ── Safety timeout ───────────────────────────────────────────────
            if (!_waiting)
            {
                _waiting       = true;
                _waitStartTime = Time.time;
            }
            else if (Time.time - _waitStartTime > MaxWaitSeconds)
            {
                // Followers couldn't collect it in time — give up.
                Debug.Log($"[WaitForPartyUpgrades] {self.name}: timed out waiting — resuming.");
                ResetState();
                return NodeState.Failure;
            }

            float distToUpgrade = Vector3.Distance(self.position, _upgradeTarget.Value);

            if (distToUpgrade > GatherRadius)
            {
                // ── Steer leader toward the drop ─────────────────────────────
                // Once within GatherRadius the leader stops and followers can reach it.
                if (_gatherGO == null)
                    _gatherGO = new GameObject("_WFPUGather")
                                { hideFlags = HideFlags.HideAndDontSave };
                _gatherGO.transform.position = _upgradeTarget.Value;
                self.GetComponent<MovementComponent>()
                    ?.OnTriggerMove(self, _gatherGO.transform, 1f);
            }
            else
            {
                // ── Close enough — hold position ─────────────────────────────
                self.GetComponent<UnitPathFollower>()?.StopPath();
                if (distToUpgrade <= GatherRadius && !Mathf.Approximately(distToUpgrade, 0f))
                    Debug.Log($"[WaitForPartyUpgrades] {self.name}: holding at upgrade drop.");
            }

            return NodeState.Running;
        }

        // Nothing useful on the ground — fall through to Explore.
        if (_waiting)
        {
            Debug.Log($"[WaitForPartyUpgrades] {self.name}: all upgrades collected — resuming.");
            ResetState();
        }
        return NodeState.Failure;
    }

    private void ResetState()
    {
        _waiting       = false;
        _upgradeTarget = null;
        if (_gatherGO != null)
        {
            Object.Destroy(_gatherGO);
            _gatherGO = null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the world position of the nearest WorldItem that is a genuine
    /// upgrade for at least one living, non-leader party member.
    /// Returns null when no such item exists.
    /// </summary>
    private Vector3? FindNearestFollowerUpgrade(Transform leader)
    {
        // Build the list of living non-leader followers once per scan.
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        var followers = new System.Collections.Generic.List<
            (Transform t, EquipmentComponent eq)>();

        foreach (GameObject playerObj in players)
        {
            if (playerObj == null || !playerObj.activeInHierarchy) continue;

            // Skip the leader — they collect their own upgrades via the Items sequence.
            if (playerObj.transform == leader) continue;

            HealthComponent hp = playerObj.GetComponent<HealthComponent>();
            if (hp != null && hp.currentHealth <= 0) continue;

            EquipmentComponent eq = playerObj.GetComponent<EquipmentComponent>();
            if (eq == null) continue;

            followers.Add((playerObj.transform, eq));
        }

        // No living followers → nothing to wait for.
        if (followers.Count == 0) return null;

        Vector3? nearest     = null;
        float    nearestDist = float.MaxValue;

        foreach (WorldItem worldItem in WorldItemRegistry.All)
        {
            if (worldItem == null || worldItem.item == null) continue;

            float distToLeader = Vector3.Distance(leader.position,
                                                  worldItem.transform.position);
            if (distToLeader > _searchRange) continue;

            // Skip hidden or non-walkable items.
            if (_fogManager != null &&
                !_fogManager.IsRevealed(worldItem.transform.position))
                continue;

            var node = GridGenerator.Instance?.GetNodeAtWorldPosition(
                            worldItem.transform.position);
            if (node == null || !node.isWalkable) continue;

            // Check each living follower.
            foreach (var (followerT, followerEq) in followers)
            {
                if (IsUpgradeFor(worldItem.item, followerEq, followerT))
                {
                    if (distToLeader < nearestDist)
                    {
                        nearestDist = distToLeader;
                        nearest     = worldItem.transform.position;
                    }
                    break; // one matching follower is enough — pick the nearest item
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Returns true when <paramref name="item"/> is a class-compatible upgrade
    /// over what <paramref name="hero"/> currently has equipped in the same slot.
    /// </summary>
    private static bool IsUpgradeFor(ItemSO item, EquipmentComponent equipment,
                                     Transform hero)
    {
        if (item is WeaponSO weapon)
        {
            var unit     = hero.GetComponent<BaseUnit>();
            var heroSO   = unit?.unitData as HeroSO;
            var advClass = heroSO?.adventurerClass;

            if (advClass != null && !advClass.CanEquipWeapon(weapon))
                return false;
        }

        float newScore     = item.GetScore();
        float currentScore = GetCurrentSlotScore(equipment, item);

        return newScore > currentScore;
    }

    private static float GetCurrentSlotScore(EquipmentComponent equipment,
                                             ItemSO candidate)
    {
        if (candidate is WeaponSO)    return equipment.equippedWeapon?.GetScore() ?? 0f;
        if (candidate is HeadArmorSO) return equipment.equippedHead?.GetScore()   ?? 0f;
        if (candidate is BodyArmorSO) return equipment.equippedBody?.GetScore()   ?? 0f;
        return 0f;
    }
}
