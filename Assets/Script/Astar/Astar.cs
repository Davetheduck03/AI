using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Astar : MonoBehaviour
{
    public static Astar Instance { get; private set; }

    public List<PathNode> allNodes = new List<PathNode>();

    public static event Action<List<PathNode>> OnPathFound;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

        public void FindPath(PathNode start, PathNode goal)
        {
            StartCoroutine(FindPathRoutine(start, goal, path => OnPathFound?.Invoke(path)));
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

                float tentativeG = current.gCost + Vector2.Distance(
                    (Vector2)current.transform.position,
                    (Vector2)neighbor.transform.position);

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