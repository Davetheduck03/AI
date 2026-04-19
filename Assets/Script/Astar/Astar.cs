using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Astar : MonoBehaviour
{
    public static Astar Instance { get; private set; }

    public List<PathNode> allNodes = new List<PathNode>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
        // 2. CALLBACK VERSION (use this when you need the path NOW)
        public void FindPath(PathNode start, PathNode goal, System.Action<List<PathNode>> onComplete)
        {
            StartCoroutine(FindPathRoutine(start, goal, onComplete));
        }

        private IEnumerator FindPathRoutine(PathNode start, PathNode goal, System.Action<List<PathNode>> onComplete)
        {
            List<PathNode> path = CalculatePath(start, goal);
            yield return null;
            onComplete?.Invoke(path);
        }
    

    // Extra gCost added per adjacent non-walkable (wall) tile neighbour.
    // This makes nodes near walls more expensive so A* naturally routes
    // through the centre of corridors.
    //
    // 0.35 was too low: a single-wall-neighbour node only cost 0.35 extra,
    // which is less than the ~1-unit detour to reach the corridor centre, so
    // short paths still hugged the wall.  1.2 makes a wall-edge node
    // (1 wall neighbour) cost 1.2 extra — more than the detour cost for any
    // realistic corridor — without making wall-adjacent tiles impassable in
    // 1-tile-wide passages where there is no penalty-free alternative.
    private const float WallProximityPenalty = 1.2f;

    private List<PathNode> CalculatePath(PathNode start, PathNode goal)
    {
        var openSet = new List<PathNode>();
        var closedSet = new HashSet<PathNode>();

        // Reset all nodes
        foreach (var node in allNodes)
        {
            node.gCost = Mathf.Infinity;
            node.parent = null;
        }

        openSet.Add(start);
        start.gCost = 0;
        start.hCost = Heuristic(start, goal);

        while (openSet.Count > 0)
        {
            var current = openSet.OrderBy(n => n.fCost).First();
            openSet.Remove(current);
            closedSet.Add(current);

            if (current == goal)
            {
                return ReconstructPath(goal); // Success!
            }

            foreach (var neighbor in current.neighbors)
            {
                if (closedSet.Contains(neighbor) || !neighbor.isWalkable)
                    continue;

                float moveCost    = Vector2.Distance(
                    (Vector2)current.transform.position,
                    (Vector2)neighbor.transform.position);

                // Penalise tiles adjacent to walls so paths prefer open space.
                // Count non-walkable entries in the neighbour's own neighbour list;
                // each one adds WallProximityPenalty to the traversal cost.
                float wallPenalty = WallAdjacentCount(neighbor) * WallProximityPenalty;

                float tentativeG = current.gCost + moveCost + wallPenalty;

                if (tentativeG < neighbor.gCost)
                {
                    neighbor.parent = current;
                    neighbor.gCost = tentativeG;
                    neighbor.hCost = Heuristic(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return new List<PathNode>();
    }

    /// <summary>
    /// Returns the number of non-walkable (wall) tiles in <paramref name="node"/>'s
    /// neighbour list.  Higher values mean the node is close to a wall.
    /// </summary>
    private static int WallAdjacentCount(PathNode node)
    {
        int walls = 0;
        foreach (var n in node.neighbors)
            if (n == null || !n.isWalkable) walls++;
        return walls;
    }

    private List<PathNode> ReconstructPath(PathNode endNode)
    {
        var path = new List<PathNode>();
        var current = endNode;

        while (current != null)
        {
            path.Add(current);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    private float Heuristic(PathNode a, PathNode b)
    {
        return Vector2.Distance((Vector2)a.transform.position, (Vector2)b.transform.position);
    }
    
}