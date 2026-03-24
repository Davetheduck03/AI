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

    private void Update()
    {
        if (root != null) root.Evaluate();
    }
}