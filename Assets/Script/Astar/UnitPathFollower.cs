using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        {
            followRoutine = StartCoroutine(FollowPath(moveSpeed));
            Debug.Log("Path Started");
        }
        else
        {
            Debug.Log("No Path");
        }
        
    }

    public void RecalculatePath(PathNode newGoal)
    {
        if (movementComp == null) return;

        PathNode currentNode = GridGenerator.Instance.GetNodeAtWorldPosition(transform.position);
        if (currentNode == null || newGoal == null) return;

        // Use CALLBACK version
        Astar.Instance.FindPath(currentNode, newGoal, (newPath) =>
        {
            SetPath(newPath, movementComp.movement_Speed, movementComp);
        });
    }

    private IEnumerator FollowPath(float moveSpeed)
    {
        PathNode.OnNodeUpdated += HandleNodeBlocked;
        Debug.Log(moveSpeed);
        while (currentIndex < path.Count)
        {
            PathNode targetNode = path[currentIndex];

            if (!targetNode.isWalkable)
            {
                RecalculatePath(path[path.Count - 1]);
                yield break;
            }

            Vector2 targetPos = new Vector2(targetNode.transform.position.x, targetNode.transform.position.y);
            float tolerance = 0.1f;

            Debug.Log($"Moving to node {currentIndex}: {targetNode.name} at {targetPos}");

            while (Vector2.Distance((Vector2)transform.position, targetPos) > tolerance)
            {
                Vector2 currentPos = (Vector2)transform.position;
                Vector2 direction = (targetPos - currentPos).normalized;

                var hit = Physics2D.Raycast(currentPos, direction, 1.5f, LayerMask.GetMask("Node"));
                Debug.DrawRay(currentPos, direction * 1.5f, Color.cyan, Time.deltaTime);

                if (hit.collider != null)
                {
                    var hitNode = hit.collider.GetComponent<PathNode>();
                    if (hitNode != null && !hitNode.isWalkable)
                    {
                        RecalculatePath(path[path.Count - 1]);
                        yield break;
                    }
                }

                Vector2 pos = transform.position;
                pos += direction * moveSpeed * Time.deltaTime;
                transform.position = pos;

                yield return null;
            }

            currentIndex++;
        }

        PathNode.OnNodeUpdated -= HandleNodeBlocked;
        Debug.Log("Path Complete!");
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
