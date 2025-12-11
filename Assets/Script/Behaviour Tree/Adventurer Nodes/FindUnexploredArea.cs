using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ACTION: Finds the nearest unexplored fog position and sets it as target.
/// Uses room commitment - completes current room before moving to another.
/// Returns Success if unexplored area found, Failure if map fully explored.
/// </summary>
public class FindUnexploredArea : Node
{
    private float maxSearchRange;
    private FogOfWarManager fogManager;

    // Room commitment settings
    private float roomRadius = 10f;  // How close tiles must be to be considered same "room"
    private Vector3? committedRoomCenter = null;
    private int remainingTilesInRoom = 0;

    public FindUnexploredArea(Blackboard bb, float range = 50f, float roomSize = 10f) : base(bb)
    {
        maxSearchRange = range;
        roomRadius = roomSize;
        fogManager = Object.FindAnyObjectByType<FogOfWarManager>();
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null || fogManager == null)
        {
            return NodeState.Failure;
        }

        // Get all unrevealed positions
        List<Vector3> unrevealedPositions = fogManager.GetUnrevealedPositions();

        if (unrevealedPositions.Count == 0)
        {
            Debug.Log("Map fully explored!");
            committedRoomCenter = null;
            return NodeState.Failure;
        }

        // Check if we're still committed to a room
        if (committedRoomCenter.HasValue)
        {
            // Count how many unrevealed tiles remain in committed room
            int tilesInRoom = unrevealedPositions.Count(pos =>
                Vector3.Distance(pos, committedRoomCenter.Value) <= roomRadius);

            if (tilesInRoom > 0)
            {
                // Still tiles in this room - find nearest one in the room
                Vector3? nearestInRoom = GetNearestInRoom(self.position, unrevealedPositions, committedRoomCenter.Value, roomRadius);

                if (nearestInRoom.HasValue)
                {
                    SetExplorationTarget(nearestInRoom.Value);
                    Debug.Log($"Continuing room exploration: {tilesInRoom} tiles remaining in room");
                    return NodeState.Success;
                }
            }

            // Room complete - release commitment
            Debug.Log("Room exploration complete! Finding new room...");
            committedRoomCenter = null;
        }

        // Not committed to a room - find nearest cluster of fog
        Vector3? nearestCluster = FindNearestFogCluster(self.position, unrevealedPositions);

        if (nearestCluster.HasValue)
        {
            float distance = Vector3.Distance(self.position, nearestCluster.Value);

            if (distance <= maxSearchRange)
            {
                // Commit to this new room
                committedRoomCenter = nearestCluster.Value;
                remainingTilesInRoom = unrevealedPositions.Count(pos =>
                    Vector3.Distance(pos, committedRoomCenter.Value) <= roomRadius);

                SetExplorationTarget(nearestCluster.Value);
                Debug.Log($"New room found at {nearestCluster.Value}, distance: {distance:F1}, tiles: {remainingTilesInRoom}");
                return NodeState.Success;
            }
        }

        Debug.Log("No unrevealed areas found in range");
        return NodeState.Failure;
    }

    /// <summary>
    /// Find the nearest fog cluster (densest area of unrevealed tiles)
    /// </summary>
    private Vector3? FindNearestFogCluster(Vector3 fromPosition, List<Vector3> unrevealedPositions)
    {
        if (unrevealedPositions.Count == 0) return null;

        Vector3? bestCluster = null;
        float bestScore = float.MinValue;

        // Check each unrevealed position as potential cluster center
        foreach (Vector3 candidate in unrevealedPositions)
        {
            // Count nearby tiles
            int nearbyTiles = unrevealedPositions.Count(pos =>
                Vector3.Distance(pos, candidate) <= roomRadius);

            float distance = Vector3.Distance(fromPosition, candidate);

            // Score: prefer dense clusters that are close
            // Higher density = higher score, closer = higher score
            float score = nearbyTiles * 10f - distance;

            if (score > bestScore)
            {
                bestScore = score;
                bestCluster = candidate;
            }
        }

        return bestCluster;
    }

    /// <summary>
    /// Get nearest unrevealed position within a specific room
    /// </summary>
    private Vector3? GetNearestInRoom(Vector3 fromPosition, List<Vector3> unrevealedPositions, Vector3 roomCenter, float radius)
    {
        Vector3? nearest = null;
        float closestDist = float.MaxValue;

        foreach (Vector3 pos in unrevealedPositions)
        {
            // Only consider tiles in this room
            if (Vector3.Distance(pos, roomCenter) <= radius)
            {
                float dist = Vector3.Distance(fromPosition, pos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    nearest = pos;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Set exploration target in blackboard
    /// </summary>
    private void SetExplorationTarget(Vector3 position)
    {
        GameObject targetObj = new GameObject("ExplorationTarget");
        targetObj.transform.position = position;
        bb.Set("target", targetObj.transform);
    }
}