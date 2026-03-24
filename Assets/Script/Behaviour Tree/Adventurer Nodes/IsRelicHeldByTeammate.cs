using UnityEngine;

/// <summary>
/// Succeeds when a teammate (not this hero) is carrying the relic.
/// Reads "relicHolder" from the team blackboard and writes the carrier's
/// Transform to the local blackboard under the key "relicHolder" so
/// MoveTowardsTarget(bb, range, "relicHolder") can be used directly after it.
///
/// Fails when:
///   - No one holds the relic (key absent or null)
///   - This hero IS the relic holder (no need to follow yourself)
/// </summary>
public class IsRelicHeldByTeammate : Node
{
    private readonly Blackboard team;

    public IsRelicHeldByTeammate(Blackboard bb, Blackboard team) : base(bb)
    {
        this.team = team;
    }

    public override NodeState Evaluate()
    {
        if (team == null) return NodeState.Failure;

        Transform holder = team.Get<Transform>("relicHolder");

        if (holder == null)                          return NodeState.Failure;
        if (holder == bb.Get<Transform>("self"))     return NodeState.Failure;  // I'm the carrier

        // Mirror into the local board so MoveTowardsTarget can reach it
        bb.Set("relicHolder", holder);
        return NodeState.Success;
    }
}
