using System.Collections.Generic;
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
        // Synchronous pathfinding — result delivered in the same frame.
        // The old coroutine version yielded one frame before invoking the callback,
        // which caused every hero to stand still for at least 16 ms on every path
        // request.  With 4 heroes continuously requesting paths this created
        // visible micro-stutters and the "pause before deciding" behaviour.
        public void FindPath(PathNode start, PathNode goal, System.Action<List<PathNode>> onComplete)
        {
            onComplete?.Invoke(CalculatePath(start, goal));
        }
    

    // Extra gCost added per adjacent non-walkable (wall) tile neighbour.
    // This makes nodes near walls more expensive so A* naturally routes
    // through the centre of corridors.
    //
    // Each wall-neighbour tile pays this cost once per step through it.
    // A corridor-edge tile (1 wall neighbour) now costs 1 + 3 = 4 per step,
    // vs 1 for an open-centre tile.  This is high enough that A* routes
    // through even a 2-step detour to stay centre rather than hug a wall,
    // while still allowing wall-adjacent tiles in 1-tile-wide corridors where
    // there is no penalty-free alternative (every tile in the corridor has
    // 2 wall neighbours, so all candidates pay the same penalty and the
    // shortest path is still chosen correctly).
    private const float WallProximityPenalty = 3.0f;

    private List<PathNode> CalculatePath(PathNode start, PathNode goal)
    {
        var openSet   = new List<PathNode>();
        var closedSet = new HashSet<PathNode>();

        // Track every node we write to so we only reset those at the end,
        // instead of iterating the entire grid (O(grid size)) on every call.
        var touched = new List<PathNode>();

        start.gCost  = 0f;
        start.hCost  = Heuristic(start, goal);
        start.parent = null;
        openSet.Add(start);
        touched.Add(start);

        while (openSet.Count > 0)
        {
            // Linear-scan for the lowest-fCost node.
            // Replaces openSet.OrderBy(n => n.fCost).First() which allocates an
            // IEnumerable and sorts the whole list on every iteration — O(n log n)
            // with GC pressure.  A plain loop is O(n) and allocation-free.
            PathNode current = openSet[0];
            for (int k = 1; k < openSet.Count; k++)
                if (openSet[k].fCost < current.fCost) current = openSet[k];

            openSet.Remove(current);
            closedSet.Add(current);

            if (current == goal)
            {
                var path = ReconstructPath(goal);
                // Clean up only the nodes we actually touched — much cheaper than
                // resetting the whole grid at the start of every call.
                foreach (var n in touched) { n.gCost = Mathf.Infinity; n.parent = null; }
                return path;
            }

            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null || closedSet.Contains(neighbor) || !neighbor.isWalkable)
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
                    // First time we write to this neighbour — track it for cleanup.
                    if (neighbor.gCost == Mathf.Infinity)
                        touched.Add(neighbor);

                    neighbor.parent = current;
                    neighbor.gCost  = tentativeG;
                    neighbor.hCost  = Heuristic(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        // No path found — still reset touched nodes so next call starts clean.
        foreach (var n in touched) { n.gCost = Mathf.Infinity; n.parent = null; }
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