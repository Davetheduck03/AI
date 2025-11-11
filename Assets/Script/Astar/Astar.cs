using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace TowerDefenseTK
{
    public class Astar : MonoBehaviour
    {
        public static Astar Instance;
        private void Awake() => Instance = this;

        public List<PathNode> allNodes = new List<PathNode>();


        public List<PathNode> FindPath(PathNode start, PathNode goal)
        {
            var openSet = new List<PathNode>();
            var closedSet = new HashSet<PathNode>();
            openSet.Add(start);

            foreach (var node in allNodes)
            {
                node.gCost = Mathf.Infinity;
                node.hCost = 0;
                node.parent = null;
            }

            start.gCost = 0;
            start.hCost = Heuristic(start, goal);

            while (openSet.Count > 0)
            {
                var current = openSet.OrderBy(n => n.fCost).First();

                openSet.Remove(current);
                closedSet.Add(current);

                foreach (var neighbor in current.neighbors)
                {
                    if (closedSet.Contains(neighbor) || !neighbor.isWalkable)
                        continue;

                    float tentativeG = current.gCost + Vector3.Distance(current.transform.position, neighbor.transform.position);

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

            return null;
        }

        private float Heuristic(PathNode a, PathNode b)
        {
            return Vector3.Distance(a.transform.position, b.transform.position);
        }

        private List<PathNode> ReconstructPath(PathNode start, PathNode goal)
        {
            List<PathNode> path = new List<PathNode>();
            PathNode current = goal;

            while (current != null)
            {
                path.Add(current);
                current = current.parent;
            }

            path.Reverse();
            return path;
        }

    }
}
