using UnityEngine;

/// <summary>
/// Composite: Selector (Fallback) - Succeeds on first Success child.
/// Higher priority = leftmost. Fails only if all fail.
/// </summary>

public class Selector : Node
{
    private int currentIndex = 0;

    public Selector(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        for (; currentIndex < children.Count; ++currentIndex)
        {
            NodeState childState = children[currentIndex].Evaluate();

            if (childState == NodeState.Success)
            {
                currentIndex = 0;
                return NodeState.Success;
            }

            if (childState == NodeState.Running)
            {
                return NodeState.Running;
            }
        }

        // All failed
        currentIndex = 0;
        return NodeState.Failure;
    }
}
