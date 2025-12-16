using UnityEngine;

/// <summary>
/// Composite: Sequence - Succeeds if ALL children succeed (in order).
/// Resets when sequence fails or completes.
/// </summary>
public class Sequence : Node
{
    private int currentIndex = 0;

    public Sequence(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        while (currentIndex < children.Count)
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

            // Success - move to next child
            currentIndex++;
        }

        // All children succeeded
        currentIndex = 0;
        return NodeState.Success;
    }

    public void Reset()
    {
        currentIndex = 0;
    }
}