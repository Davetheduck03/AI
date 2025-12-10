using UnityEngine;

/// <summary>
/// FIXED: Registers exploration targets to prevent oscillation.
/// Works with FogOfWarManager to avoid re-targeting recently visited areas.
/// </summary>
public class FindUnexploredArea : Node
{
    private float maxSearchRange;
    private FogOfWarManager fogManager;

    public FindUnexploredArea(Blackboard bb, float range = 50f) : base(bb)
    {
        maxSearchRange = range;
        fogManager = Object.FindAnyObjectByType<FogOfWarManager>();

        if (fogManager == null)
        {
            Debug.LogWarning("FindUnexploredArea: No FogOfWarManager found!");
        }
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null)
        {
            Debug.LogError("FindUnexploredArea: Self is null!");
            return NodeState.Failure;
        }

        if (fogManager == null)
        {
            Debug.LogError("FindUnexploredArea: FogOfWarManager is null!");
            return NodeState.Failure;
        }

        // Clean up old exploration target if it exists
        Transform oldTarget = bb.Get<Transform>("target");
        if (oldTarget != null && oldTarget.gameObject.name == "ExplorationTarget")
        {
            Object.Destroy(oldTarget.gameObject);
        }

        // Find nearest unrevealed position (now avoids recent targets)
        Vector3? nearestUnrevealed = fogManager.GetNearestUnrevealedPosition(self.position);

        if (nearestUnrevealed.HasValue)
        {
            float distance = Vector3.Distance(self.position, nearestUnrevealed.Value);

            if (distance <= maxSearchRange)
            {
                // Register this target to prevent re-targeting it soon
                fogManager.RegisterExplorationTarget(nearestUnrevealed.Value);

                // Create new exploration target
                GameObject targetObj = new GameObject("ExplorationTarget");
                targetObj.transform.position = nearestUnrevealed.Value;

                bb.Set("target", targetObj.transform);

                Debug.Log($"FindUnexploredArea: SUCCESS - Target {nearestUnrevealed.Value}, dist: {distance:F1}");
                return NodeState.Success;
            }
            else
            {
                Debug.Log($"FindUnexploredArea: Area too far ({distance:F1} > {maxSearchRange})");
            }
        }
        else
        {
            Debug.Log("FindUnexploredArea: No unrevealed areas found - map fully explored!");
        }

        return NodeState.Failure;
    }
}