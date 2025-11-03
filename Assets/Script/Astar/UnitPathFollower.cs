using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefenseTK
{
    public class UnitPathFollower : MonoBehaviour
    {

        private List<PathNode> path;
        private int currentIndex = 0;
        private Coroutine followRoutine;
        private MovementComponent movementComp;

        public void SetPath(List<PathNode> newPath, float moveSpeed, MovementComponent mc = null)
        {
            path = newPath;
            currentIndex = 0;
            movementComp = mc;
            StopAllCoroutines();

            if (path != null && path.Count > 0)
                followRoutine = StartCoroutine(FollowPath(moveSpeed));

            Debug.Log("Path Started");
        }

        /// <summary>
        /// Recalculates path from current position to the original goal.
        /// </summary>
        public void RecalculatePath(PathNode newGoal)
        {
            if (movementComp == null) return;

            PathNode currentNode = NodeGetter.GetNodeBelow(transform.position + Vector3.up * 0.5f,
                                                           movementComp.nodeLayer);
            if (currentNode == null || newGoal == null) return;

            var newPath = Astar.Instance.FindPath(currentNode, newGoal);
            SetPath(newPath, movementComp.movement_Speed, movementComp);
        }


        private IEnumerator FollowPath(float moveSpeed)
        {
            PathNode.OnNodeUpdated += HandleNodeBlocked;

            while (currentIndex < path.Count)
            {
                PathNode targetNode = path[currentIndex];

                if (!targetNode.isWalkable)
                {
                    PathNode goal = path[path.Count - 1];
                    RecalculatePath(goal);
                    yield break;
                }

                Vector3 targetPos = targetNode.transform.position;


                while (Vector3.Distance(transform.position, targetPos) > 0.15f)
                {
                    if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                                        (targetPos - transform.position).normalized,
                                        out RaycastHit hit, 1.5f, movementComp?.nodeLayer ?? 0))
                    {
                        PathNode blocked = hit.collider.GetComponent<PathNode>();
                        if (blocked != null && !blocked.isWalkable)
                        {
                            RecalculatePath(path[path.Count - 1]);
                            yield break;
                        }
                    }

                    transform.position = Vector3.MoveTowards(transform.position, targetPos,
                                                            moveSpeed * Time.deltaTime);
                    yield return null;
                }

                currentIndex++;
            }
            PathNode.OnNodeUpdated -= HandleNodeBlocked;
        }

        private void HandleNodeBlocked(PathNode blockedNode)
        {
            if (path != null && currentIndex < path.Count && path.IndexOf(blockedNode, currentIndex) != -1)
            {
                PathNode goal = path[path.Count - 1];
                RecalculatePath(goal);
            }
        }
    }
}