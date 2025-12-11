using UnityEngine;

/// <summary>
/// ACTION: Idle behavior - enemy waits at spawn position.
/// Returns Running to keep the behavior tree active.
/// </summary>
public class IdleAtSpawn : Node
{
    private float idleWaitTime = 0.5f;
    private float lastIdleTime = 0f;

    public IdleAtSpawn(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        // Simple idle - just succeed after brief wait
        if (Time.time - lastIdleTime > idleWaitTime)
        {
            lastIdleTime = Time.time;
            return NodeState.Success;
        }
        
        return NodeState.Running;
    }
}
