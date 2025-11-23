using UnityEngine;

/// <summary>
/// Decorator: Inverter - Flips Success/Failure of child (Running unchanged).
/// </summary>
public class Inverter : Node
{
    public Inverter(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        if (children.Count != 1) return NodeState.Failure;

        NodeState childState = children[0].Evaluate();
        return childState == NodeState.Success ? NodeState.Failure :
               childState == NodeState.Failure ? NodeState.Success :
               NodeState.Running;
    }
}
