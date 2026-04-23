/// <summary>
/// CONDITION: Returns Success when the party leader is actively engaged in combat
/// AND this hero is NOT the leader (i.e. is a follower).
///
/// Used as an Inverter child in the Flee sequence:
///   Inverter(IsLeaderInCombat) → Success when leader is NOT fighting (flee proceeds normally)
///                              → Failure when leader IS fighting   (flee suppressed)
///
/// When the flee sequence is blocked, the hero falls through to the Attack sequence
/// which already picks up the leader's combat target via SelectCombatTarget.
/// The net effect: followers override their personal fear and rush to help the leader.
///
/// The leader's own flee is never suppressed — IsLeaderInCombat returns Failure for
/// the leader so their Inverter gate always passes and personal flee runs normally.
/// </summary>
public class IsLeaderInCombat : Node
{
    public IsLeaderInCombat(Blackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        var self = bb.Get<UnityEngine.Transform>("self");
        if (self == null) return NodeState.Failure;

        // Leaders' flee is never suppressed — they are free to retreat.
        if (FormationManager.Instance?.IsLeader(self) == true)
            return NodeState.Failure;

        // Is there a live team combat target?
        var target = TeamBlackboard.Instance?.Get<UnityEngine.Transform>("leaderCombatTarget");
        if (target == null || target.gameObject == null) return NodeState.Failure;

        var hp = target.GetComponent<HealthComponent>();
        if (hp != null && hp.currentHealth <= 0f) return NodeState.Failure;

        // Leader has a live target and this hero is a follower → signal "leader in combat"
        return NodeState.Success;
    }
}
