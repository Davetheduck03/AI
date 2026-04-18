using UnityEngine;

/// <summary>
/// Succeeds when the party leader has an active combat target, copying it into
/// this hero's local blackboard so <see cref="AdaptiveAttack"/> can engage immediately.
///
/// Purpose: followers that are just outside their own detection range (e.g. one tile
/// behind the leader in a corridor fight) would normally fall through to FollowLeader
/// and stand idle behind the fight.  This node gives them the leader's target directly
/// so they rush in and help instead.
///
/// Place this sequence just BEFORE FollowLeader in every damage-dealing AI tree.
/// </summary>
public class AssistLeaderInCombat : Node
{
    public AssistLeaderInCombat(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        // The leader drives its own attack sequence — no self-assistance needed.
        if (FormationManager.Instance?.IsLeader(self) == true)
            return NodeState.Failure;

        // Read the leader's current combat target off the shared team board.
        Transform leaderTarget = TeamBlackboard.Instance?.Get<Transform>("leaderCombatTarget");

        if (leaderTarget == null) return NodeState.Failure;

        // Guard against stale references (enemy died, GO destroyed).
        if (leaderTarget.gameObject == null) return NodeState.Failure;

        // Only assist against living enemies.
        var hp = leaderTarget.GetComponent<HealthComponent>();
        if (hp != null && hp.currentHealth <= 0) return NodeState.Failure;

        bb.Set("target", leaderTarget);
        return NodeState.Success;
    }
}
