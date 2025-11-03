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

        public Dictionary<(PathNode, PathNode), List<PathNode>> generatedPathCache = new Dictionary<(PathNode, PathNode), List<PathNode>>();
        public Dictionary<(PathNode, PathNode), List<PathNode>> customPathCache = new Dictionary<(PathNode, PathNode), List<PathNode>>();


        private void OnEnable()
        {
            GridGenerator.OnGridGenerated += DelayComputePath;
        }

        private void OnDisable()
        {
            GridGenerator.OnGridGenerated -= DelayComputePath;
        }

        private void PrecomputeAllPaths()
        {
            foreach (var startNode in NodeGetter.nodeValue[NodeType.Start])
            {
                foreach (var endNode in NodeGetter.nodeValue[NodeType.End])
                {
                    if (startNode != null && endNode != null)
                    {
                        var path = FindPath(startNode, endNode);
                        if (path != null)
                        {
                            generatedPathCache[(startNode, endNode)] = path;
                        }
                    }
                }
            }
        }

        private void DelayComputePath()
        {
            StartCoroutine(C_DelayComputePath());
        }

        private IEnumerator C_DelayComputePath()
        {
            yield return new WaitForEndOfFrame();
            PrecomputeAllPaths();
        }

        public List<PathNode> FindPath(PathNode start, PathNode goal)
        {
            var key = (start, goal);
            if (generatedPathCache.ContainsKey(key))
            {
                return generatedPathCache[key];
            }

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

                if (current == goal)
                {
                    var path = ReconstructPath(start, goal);
                    generatedPathCache[key] = path;
                    return path;
                }

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

        public void ClearCache()
        {
            generatedPathCache.Clear();
        }
    }
}
