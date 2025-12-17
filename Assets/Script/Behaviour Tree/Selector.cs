using UnityEngine;

/// <summary>
/// Composite: Selector (Fallback) - Succeeds on first Success child.
/// REACTIVE: Always starts from first child to ensure priority is respected.
/// </summary>
public class Selector : Node
{
    public Selector(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        for (int i = 0; i < children.Count; i++)
        {
            NodeState childState = children[i].Evaluate();

            if (childState == NodeState.Success)
            {
                return NodeState.Success;
            }

            if (childState == NodeState.Running)
            {
                return NodeState.Running;
            }
        }

        return NodeState.Failure;
    }
}
