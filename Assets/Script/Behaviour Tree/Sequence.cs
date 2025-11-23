using UnityEngine;

/// <summary>
/// Composite: Sequence - Succeeds if ALL children succeed (in order).
/// Remembers progress across ticks.
/// </summary>
public class Sequence : Node
{
    private int currentIndex = 0;

    public Sequence(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        // Resume from last index
        for (; currentIndex < children.Count; ++currentIndex)
        {
            NodeState childState = children[currentIndex].Evaluate();

            if (childState == NodeState.Failure)
            {
                currentIndex = 0;
                return NodeState.Failure;
            }

            if (childState == NodeState.Running)
            {
                return NodeState.Running;
            }
        }

        // All children succeeded
        currentIndex = 0;
        return NodeState.Success;
    }
}