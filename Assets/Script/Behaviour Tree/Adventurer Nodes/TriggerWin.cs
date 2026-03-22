using UnityEngine;

/// <summary>
/// ACTION: Fires the Win game state when the hero reaches the extraction point.
/// This node is placed at the end of the extraction Sequence, after
/// MoveTowardsTarget has returned Success (i.e. the hero is close enough).
///
/// Returns Success immediately so the Sequence completes cleanly.
/// </summary>
public class TriggerWin : Node
{
    public TriggerWin(Blackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        if (GameManager.Instance != null)
        {
            Debug.Log("[TriggerWin] Hero reached extraction point — triggering Win state.");
            GameManager.Instance.SwitchState(GameManager.Instance.Win);
        }
        else
        {
            Debug.LogWarning("[TriggerWin] No GameManager found!");
        }

        return NodeState.Success;
    }
}
