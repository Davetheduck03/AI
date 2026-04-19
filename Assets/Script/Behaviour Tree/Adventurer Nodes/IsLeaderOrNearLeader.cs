using UnityEngine;

/// <summary>
/// CONDITION: Gates opportunistic behaviours (looting, item pick-ups) so that
/// followers only pursue them when they are already close to the leader.
///
/// Returns Success when:
///   • This hero IS the leader  → leaders always act freely.
///   • This hero is a follower AND within <see cref="nearRange"/> of the leader.
///
/// Returns Failure when:
///   • FormationManager is unavailable (defensive fallback — let other nodes decide).
///   • This hero is a follower AND farther than nearRange from the leader.
///
/// Typical use: insert as the first child of every loot / world-item Sequence in
/// follower AI classes.  When the follower is out of range it falls through to
/// FollowLeader (which runs next in the parent Selector) instead of sprinting
/// away from the group to grab a chest.
/// </summary>
public class IsLeaderOrNearLeader : Node
{
    // Followers may pursue opportunistic loot only when within this distance of
    // the leader.  Set comfortably above FollowLeader.ResumeRange (3 u) but
    // tight enough to prevent followers from straying across the room.
    // 7 u ≈ 3–4 tiles, which covers incidental loot that appears at the group's
    // feet while still blocking distant diversions.
    private readonly float nearRange;

    public IsLeaderOrNearLeader(Blackboard bb, float nearRange = 7f) : base(bb)
    {
        this.nearRange = nearRange;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        FormationManager fm = FormationManager.Instance;
        if (fm == null) return NodeState.Failure;

        // Leaders always proceed — they set their own pace.
        if (fm.IsLeader(self)) return NodeState.Success;

        // Follower: only proceed when close to the leader.
        Transform leader = fm.GetLeader();
        if (leader == null) return NodeState.Failure;

        float dist = Vector3.Distance(self.position, leader.position);
        return dist <= nearRange ? NodeState.Success : NodeState.Failure;
    }
}
