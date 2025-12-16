using UnityEngine;

/// <summary>
/// Composite: Selector (Fallback) - Succeeds on first Success child.
/// REACTIVE: Always starts from first child to ensure priority is respected.
/// </summary>
public class Selector : Node
{
    private int runningIndex = -1;

    public Selector(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        // Always start from beginning to check highest priority first
        // But remember if we had a Running child
        for (int i = 0; i < children.Count; i++)
        {
            NodeState childState = children[i].Evaluate();

            if (childState == NodeState.Success)
            {
                runningIndex = -1;
                return NodeState.Success;
            }

            if (childState == NodeState.Running)
            {
                runningIndex = i;
                return NodeState.Running;
            }
        }

        runningIndex = -1;
        return NodeState.Failure;
    }
}