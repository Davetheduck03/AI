using UnityEngine;

/// <summary>
/// Simplified: Checks if target is in revealed area.
/// Returns Success if revealed, Failure if still in fog.
/// </summary>
public class IsTargetRevealed : Node
{
    private FogOfWarManager fogManager;

    public IsTargetRevealed(Blackboard bb) : base(bb)
    {
        fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        Transform target = bb.Get<Transform>("target");

        if (target == null || fogManager == null)
        {
            return NodeState.Failure;
        }

        bool isRevealed = fogManager.IsRevealed(target.position);

        return isRevealed ? NodeState.Success : NodeState.Failure;
    }
}