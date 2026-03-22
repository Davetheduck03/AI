using UnityEngine;

/// <summary>
/// CONDITION/ACTION: Checks whether the hero is carrying the Relic.
/// If yes  → sets bb["target"] to the ExtractionPoint transform and returns Success,
///           allowing the following MoveTowardsTarget node to navigate there.
/// If no   → returns Failure, so the Selector falls through to normal combat/loot/explore.
///
/// Place this as the first node in the extraction Sequence so the whole sequence
/// is skipped when the hero has not yet found the Relic.
/// </summary>
public class SetExtractionTarget : Node
{
    public SetExtractionTarget(Blackboard blackboard) : base(blackboard) { }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        RelicHolder holder = self.GetComponent<RelicHolder>();
        if (holder == null || !holder.HasRelic)
            return NodeState.Failure;

        ExtractionPoint exit = ExtractionPoint.Instance;
        if (exit == null)
        {
            Debug.LogWarning("[SetExtractionTarget] No ExtractionPoint in scene!");
            return NodeState.Failure;
        }

        bb.Set<Transform>("target", exit.transform);
        return NodeState.Success;
    }
}
