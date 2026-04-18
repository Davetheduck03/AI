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

    // How many consecutive Failure ticks must occur before we stop movement.
    // SelectCombatTarget is throttled to 0.2 s, so on the tick right after a target
    // dies the cached result is gone and the BT briefly returns Failure before the
    // next sequence (FollowLeader / Explore) can pick up.  With a 60 fps update loop
    // that transition window can be 2-4 frames.  Raising the threshold to 8 (≈ 0.13 s)
    // bridges those blips without letting a genuinely idle hero drift indefinitely.
    private const int FailureStopThreshold = 8;
    private int _consecutiveFailures = 0;

    private void Update()
    {
        if (root == null) return;

        NodeState result = root.Evaluate();

        // If every priority in the tree failed (no enemies, no items, no fog,
        // no extraction) stop any lingering movement so the hero doesn't drift
        // toward a stale destination while the BT has nothing to drive it.
        if (result == NodeState.Failure)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= FailureStopThreshold)
            {
                var pf = GetComponent<UnitPathFollower>();
                if (pf != null) pf.StopPath();
            }
        }
        else
        {
            _consecutiveFailures = 0;
        }
    }
}