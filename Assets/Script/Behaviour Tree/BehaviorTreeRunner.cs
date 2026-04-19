using UnityEngine;

public class BehaviorTreeRunner : MonoBehaviour
{
    private Node root;

    /// <summary>Per-hero private blackboard — local state only this hero reads/writes.</summary>
    protected Blackboard bb;

    /// <summary>
    /// Shared team blackboard — all heroes can read and write this.
    /// Null-safe: always check TeamBlackboard.Instance before using if you need
    /// the MonoBehaviour, or just use this reference for raw key access.
    /// </summary>
    protected Blackboard team => TeamBlackboard.Instance?.shared;

    [Header("Config - Override in child classes or Inspector")]
    public Transform target;  // e.g., player

    protected virtual void Start()
    {
        bb = new Blackboard();
        bb.Set("self", transform);

        // Register on the team board so other heroes know where this one is
        TeamBlackboard.Instance?.Set("hero_" + gameObject.GetInstanceID(), transform);

        root = BuildTree();
    }

    protected virtual void OnDestroy()
    {
        // Remove this hero's team-board entry when they die / are cleaned up
        TeamBlackboard.Instance?.Remove("hero_" + gameObject.GetInstanceID());
    }

    protected virtual Node BuildTree()
    {
        // Override in subclasses or build dynamically
        return null;  // Placeholder
    }

    // How many consecutive Failure ticks must occur before the fallback fires.
    // SelectCombatTarget is throttled to 0.2 s, so on the tick right after a target
    // dies the cached result is gone and the BT briefly returns Failure before the
    // next sequence (FollowLeader / Explore) can pick up.  With a 60 fps update loop
    // that transition window is 2-4 frames.  8 frames (≈ 0.13 s) bridges those blips.
    private const int FailureStopThreshold = 8;
    private int _consecutiveFailures = 0;

    // Reusable GO used as the leader-path target when the BT hard-fails for a follower.
    private GameObject _btFallbackGO = null;

    private void Update()
    {
        if (root == null) return;

        NodeState result = root.Evaluate();

        // If every priority in the tree failed, apply a fallback:
        //
        //   FOLLOWERS — path straight to the leader at catch-up speed.
        //     FollowLeader normally prevents this (it always returns Running for
        //     a valid follower), but if something goes wrong (leader briefly null,
        //     FormationManager not ready, etc.) this hard-fallback keeps the hero
        //     from stopping indefinitely in a distant room.
        //
        //   LEADER / solo hero — stop any lingering movement so the hero doesn't
        //     drift toward a stale destination while the BT has nothing to drive it.
        if (result == NodeState.Failure)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= FailureStopThreshold)
            {
                _consecutiveFailures = 0;   // reset so we re-evaluate in FailureStopThreshold frames

                var fm     = FormationManager.Instance;
                bool isFollower = fm != null && !fm.IsLeader(transform);
                Transform leader = isFollower ? fm.GetLeader() : null;

                if (leader != null)
                {
                    // Follower with a live leader — path to them as hard fallback.
                    if (_btFallbackGO == null)
                        _btFallbackGO = new GameObject("_BtFallback")
                                        { hideFlags = HideFlags.HideAndDontSave };
                    _btFallbackGO.transform.position = leader.position;
                    GetComponent<MovementComponent>()?.OnTriggerMove(
                        transform, _btFallbackGO.transform, 1.45f);
                }
                else
                {
                    // Leader or no leader found — just stop stale movement.
                    GetComponent<UnitPathFollower>()?.StopPath();
                }
            }
        }
        else
        {
            _consecutiveFailures = 0;
        }
    }
}