using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitPathFollower : MonoBehaviour
{
    private List<PathNode> path;
    private int currentIndex = 0;
    private MovementComponent movementComp;

    private bool _nodeListenerActive = false;

    // How long to wait for a blocker to move before pushing through.
    private const float BlockWaitTime = 0.4f;
    private const float BlockPushThrough = 1.2f;   // give up waiting after this long total

    public void SetPath(List<PathNode> newPath, float moveSpeed, MovementComponent mc = null)
    {
        if (this == null || !this) return;

        path = newPath;
        currentIndex = 0;
        movementComp = mc;

        if (gameObject != null && gameObject.activeInHierarchy)
            StopAllCoroutines();

        if (path != null && path.Count > 0)
        {
            StartCoroutine(FollowPath(moveSpeed));
            Debug.Log("Path Started");
        }
        else
        {
            Debug.Log("No Path");
        }
    }

    public void RecalculatePath(PathNode newGoal)
    {
        if (this == null || !this) return;
        if (movementComp == null) return;
        if (newGoal == null || !newGoal) return;

        PathNode currentNode = GridGenerator.Instance.GetNodeAtWorldPosition(transform.position);
        if (currentNode == null) return;

        Astar.Instance.FindPath(currentNode, newGoal, (newPath) =>
        {
            if (this == null || !this) return;
            SetPath(newPath, movementComp.movement_Speed, movementComp);
        });
    }

    private void OnDisable()
    {
        UnsubscribeNodeListener();
    }

    private void SubscribeNodeListener()
    {
        if (!_nodeListenerActive)
        {
            PathNode.OnNodeUpdated += HandleNodeBlocked;
            _nodeListenerActive = true;
        }
    }

    private void UnsubscribeNodeListener()
    {
        if (_nodeListenerActive)
        {
            PathNode.OnNodeUpdated -= HandleNodeBlocked;
            _nodeListenerActive = false;
        }
    }

    private IEnumerator FollowPath(float moveSpeed)
    {
        // Stagger start slightly based on instance ID so units that spawn
        // simultaneously don't all request paths on the exact same frame.
        float stagger = (gameObject.GetInstanceID() & 0xF) * 0.02f;
        if (stagger > 0f) yield return new WaitForSeconds(stagger);

        SubscribeNodeListener();

        while (currentIndex < path.Count)
        {
            PathNode targetNode = path[currentIndex];

            // Grid was regenerated — all old nodes destroyed.
            if (targetNode == null || !targetNode)
            {
                Debug.Log("UnitPathFollower: path node destroyed (grid regen) — stopping.");
                UnsubscribeNodeListener();
                yield break;
            }

            if (!targetNode.isWalkable)
            {
                RecalculatePath(path[path.Count - 1]);
                yield break;
            }

            Vector2 targetPos = (Vector2)targetNode.transform.position;
            const float tolerance = 0.25f;

            // ── Blocker handling before we start moving toward this node ──────
            // If another unit is already standing on the target tile, wait up to
            // BlockPushThrough seconds for them to leave.  After that, push through
            // (move to the tile anyway) instead of re-pathing endlessly — this
            // breaks the corridor deadlock where two units repath into each other.
            if (IsBlockedByUnit(targetNode))
            {
                float waitedFor = 0f;
                bool pushedThrough = false;

                while (IsBlockedByUnit(targetNode))
                {
                    if (targetNode == null || !targetNode)
                    {
                        UnsubscribeNodeListener();
                        yield break;
                    }

                    yield return new WaitForSeconds(BlockWaitTime);
                    waitedFor += BlockWaitTime;

                    if (waitedFor >= BlockPushThrough)
                    {
                        // Blocker hasn't moved — try a one-shot repath first.
                        // If the repath produces a different next node we'll take
                        // it; if not, just push through to avoid deadlock.
                        PathNode goal = path[path.Count - 1];
                        if (goal != null && goal && goal != targetNode)
                        {
                            RecalculatePath(goal);
                            yield break;
                        }

                        // No better path — push through.
                        Debug.Log($"[UnitPathFollower] {name} pushing through blocker on {targetNode.name}");
                        pushedThrough = true;
                        break;
                    }
                }

                // Node may have been destroyed while we were waiting.
                if (!pushedThrough && (targetNode == null || !targetNode))
                {
                    UnsubscribeNodeListener();
                    yield break;
                }
            }

            // ── Move toward the node ──────────────────────────────────────────
            while (Vector2.Distance((Vector2)transform.position, targetPos) > tolerance)
            {
                if (targetNode == null || !targetNode)
                {
                    Debug.Log("UnitPathFollower: node destroyed mid-move — stopping.");
                    UnsubscribeNodeListener();
                    yield break;
                }

                Vector2 currentPos = (Vector2)transform.position;
                Vector2 direction = (targetPos - currentPos).normalized;

                // Structural obstacle raycast (walls / unwalkable nodes).
                var hit = Physics2D.Raycast(currentPos, direction, 1.5f,
                                            LayerMask.GetMask("Node"));
                Debug.DrawRay(currentPos, direction * 1.5f, Color.cyan, Time.deltaTime);

                if (hit.collider != null)
                {
                    var hitNode = hit.collider.GetComponent<PathNode>();
                    if (hitNode != null && !hitNode.isWalkable)
                    {
                        PathNode goal = path[path.Count - 1];
                        if (goal != null && goal)
                            RecalculatePath(goal);
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

        UnsubscribeNodeListener();
        Debug.Log("Path Complete!");
    }

    /// <summary>
    /// True when another unit on the Player layer is standing on <paramref name="node"/>.
    /// Also checks the Enemy layer so heroes don't deadlock against enemies either.
    /// </summary>
    private bool IsBlockedByUnit(PathNode node)
    {
        if (node == null || !node) return false;

        const float checkRadius = 0.35f;
        int mask = LayerMask.GetMask("Player", "Enemy");

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            node.transform.position, checkRadius, mask);

        foreach (var col in hits)
        {
            if (col == null) continue;
            if (col.gameObject != gameObject)
                return true;
        }
        return false;
    }

    private void HandleNodeBlocked(PathNode blockedNode)
    {
        if (this == null || !this) return;
        if (blockedNode == null || !blockedNode) return;

        if (path != null && currentIndex < path.Count &&
            path.IndexOf(blockedNode, currentIndex) != -1)
        {
            PathNode goal = path[path.Count - 1];
            if (goal != null && goal)
                RecalculatePath(goal);
        }
    }
}