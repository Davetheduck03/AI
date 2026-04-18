using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitPathFollower : MonoBehaviour
{
    private List<PathNode> path;
    private int currentIndex = 0;
    private MovementComponent movementComp;

    private bool _nodeListenerActive = false;

    /// <summary>
    /// True while a FollowPath coroutine is actively running.
    /// External systems (e.g. FollowLeader) can read this to avoid
    /// restarting a perfectly good path unnecessarily.
    /// </summary>
    public bool IsFollowingPath { get; private set; }

    /// <summary>
    /// Stops the active path coroutine and clears <see cref="IsFollowingPath"/>.
    /// Prefer this over calling StopAllCoroutines() directly so the flag stays accurate.
    /// </summary>
    public void StopPath()
    {
        IsFollowingPath = false;
        if (gameObject != null && gameObject.activeInHierarchy)
            StopAllCoroutines();
    }

    // How long to wait for a blocker to move before pushing through (high-priority hero).
    private const float BlockWaitTime   = 0.3f;
    private const float BlockPushThrough = 0.9f;   // give up waiting after this long total

    // Low-priority (yielding) hero: poll this often while waiting for the tile to clear.
    private const float YieldPollInterval = 0.2f;
    private const float MaxYieldTime      = 3.0f;   // give up yielding after this long

    // Path smoothing + corner lookahead.
    // Heroes start curving toward the next waypoint when within this distance of the current one.
    private const float LookAheadDist = 0.8f;

    // Ally avoidance steering.
    // Heroes within AvoidRadius that are roughly ahead get a lateral steer applied.
    private const float AvoidRadius   = 1.1f;
    private const float AvoidStrength = 0.8f;

    public void SetPath(List<PathNode> newPath, float moveSpeed, MovementComponent mc = null)
    {
        if (this == null || !this) return;

        path = SmoothenPath(newPath);
        currentIndex = 0;
        movementComp = mc;

        if (gameObject != null && gameObject.activeInHierarchy)
        {
            IsFollowingPath = false;
            StopAllCoroutines();
        }

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
        IsFollowingPath = true;

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
                IsFollowingPath = false;
                UnsubscribeNodeListener();
                yield break;
            }

            if (!targetNode.isWalkable)
            {
                IsFollowingPath = false;
                RecalculatePath(path[path.Count - 1]);
                yield break;
            }

            Vector2 targetPos = (Vector2)targetNode.transform.position;
            const float tolerance = 0.4f;

            // ── Blocker handling before we start moving toward this node ──────
            // Priority is determined by instance ID: lower ID = higher priority.
            //
            // HIGH-PRIORITY hero (lower instance ID than the blocker):
            //   Wait up to BlockPushThrough seconds for the blocker to move.
            //   If they don't, try one repath then push through to break deadlock.
            //
            // LOW-PRIORITY hero (higher instance ID than the blocker):
            //   Yield — stop and poll until the tile clears, giving the high-priority
            //   hero right-of-way.  After MaxYieldTime, repath to find another route.
            //   This asymmetric behaviour prevents both heroes from retriggering A*
            //   simultaneously into each other, which was the corridor deadlock.
            Collider2D blockerCol = GetBlockerCollider(targetNode);
            if (blockerCol != null)
            {
                bool isHighPriority = gameObject.GetInstanceID() < blockerCol.gameObject.GetInstanceID();

                if (isHighPriority)
                {
                    // ── High-priority: short wait then push through ─────────────
                    float waitedFor   = 0f;
                    bool  pushedThrough = false;

                    while (GetBlockerCollider(targetNode) != null)
                    {
                        if (targetNode == null || !targetNode)
                        {
                            IsFollowingPath = false;
                            UnsubscribeNodeListener();
                            yield break;
                        }

                        yield return new WaitForSeconds(BlockWaitTime);
                        waitedFor += BlockWaitTime;

                        if (waitedFor >= BlockPushThrough)
                        {
                            // Try a one-shot repath first; if no better route, push through.
                            PathNode goal = path[path.Count - 1];
                            if (goal != null && goal && goal != targetNode)
                            {
                                IsFollowingPath = false;
                                RecalculatePath(goal);
                                yield break;
                            }

                            Debug.Log($"[UnitPathFollower] {name} pushing through blocker on {targetNode.name}");
                            pushedThrough = true;
                            break;
                        }
                    }

                    if (!pushedThrough && (targetNode == null || !targetNode))
                    {
                        IsFollowingPath = false;
                        UnsubscribeNodeListener();
                        yield break;
                    }
                }
                else
                {
                    // ── Low-priority: yield, giving right-of-way ───────────────
                    // Poll until the tile is clear or we hit MaxYieldTime, then repath.
                    float yieldedFor = 0f;
                    bool  tileCleared = false;

                    while (yieldedFor < MaxYieldTime)
                    {
                        if (targetNode == null || !targetNode)
                        {
                            IsFollowingPath = false;
                            UnsubscribeNodeListener();
                            yield break;
                        }

                        yield return new WaitForSeconds(YieldPollInterval);
                        yieldedFor += YieldPollInterval;

                        if (GetBlockerCollider(targetNode) == null)
                        {
                            tileCleared = true;
                            break;
                        }
                    }

                    if (!tileCleared)
                    {
                        // Blocker is still there — repath around them.
                        PathNode goal = path[path.Count - 1];
                        if (goal != null && goal && goal != targetNode)
                        {
                            Debug.Log($"[UnitPathFollower] {name} yielded {yieldedFor:F1}s — repathing around blocker");
                            IsFollowingPath = false;
                            RecalculatePath(goal);
                            yield break;
                        }
                    }
                }
            }

            // ── Move toward the node ──────────────────────────────────────────
            while (Vector2.Distance((Vector2)transform.position, targetPos) > tolerance)
            {
                if (targetNode == null || !targetNode)
                {
                    Debug.Log("UnitPathFollower: node destroyed mid-move — stopping.");
                    IsFollowingPath = false;
                    UnsubscribeNodeListener();
                    yield break;
                }

                Vector2 currentPos = (Vector2)transform.position;

                // ── Corner lookahead ──────────────────────────────────────────
                // When close to the current waypoint, blend the aim point toward
                // the next one.  This pre-curves the direction so the hero rounds
                // corners smoothly rather than hard-pivoting exactly at each node.
                Vector2 lookaheadTarget = targetPos;
                if (currentIndex + 1 < path.Count &&
                    path[currentIndex + 1] != null && path[currentIndex + 1])
                {
                    float distToNode = Vector2.Distance(currentPos, targetPos);
                    if (distToNode < LookAheadDist)
                    {
                        float blend = (1f - distToNode / LookAheadDist) * 0.55f;
                        lookaheadTarget = Vector2.Lerp(targetPos,
                            (Vector2)path[currentIndex + 1].transform.position, blend);
                    }
                }

                Vector2 direction = (lookaheadTarget - currentPos).normalized;

                // ── Ally avoidance steering ───────────────────────────────────
                // Steers laterally around nearby heroes so they lane-change past
                // each other instead of colliding and cancelling movement.
                Vector2 avoidance = ComputeAvoidanceSteering(currentPos, direction);
                if (avoidance.sqrMagnitude > 0.001f)
                    direction = (direction + avoidance).normalized;

                // Structural obstacle raycast (walls / unwalkable nodes).
                var hit = Physics2D.Raycast(currentPos, direction, 1.5f,
                                            LayerMask.GetMask("Node"));
                Debug.DrawRay(currentPos, direction * 1.5f, Color.cyan, Time.deltaTime);

                if (hit.collider != null)
                {
                    var hitNode = hit.collider.GetComponent<PathNode>();
                    if (hitNode != null && !hitNode.isWalkable)
                    {
                        IsFollowingPath = false;
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

        IsFollowingPath = false;
        UnsubscribeNodeListener();
        Debug.Log("Path Complete!");
    }

    /// <summary>
    /// Computes a lateral steering offset that nudges this hero away from nearby allies
    /// when they are on a collision course.
    ///
    /// Works by projecting each nearby hero onto the perpendicular-to-movement axis
    /// and steering to the opposite side.  The result is a natural lane-change: heroes
    /// flowing past each other rather than bouncing or stalling head-on.
    ///
    /// Tie-break for exactly-ahead collisions uses the instance ID so the two heroes
    /// always pick opposite sides deterministically.
    /// </summary>
    private Vector2 ComputeAvoidanceSteering(Vector2 pos, Vector2 moveDir)
    {
        int mask = LayerMask.GetMask("Player");
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, AvoidRadius, mask);

        Vector2 lateral = new Vector2(-moveDir.y, moveDir.x); // left-perpendicular
        Vector2 steer   = Vector2.zero;

        foreach (var col in hits)
        {
            if (col == null || col.gameObject == gameObject) continue;

            Vector2 toOther = (Vector2)col.transform.position - pos;
            float   dist    = toOther.magnitude;

            float ahead, lateralOff;

            if (dist < 0.001f)
            {
                // Exact overlap — deterministic split so each hero picks a different side.
                ahead      = 1f;
                lateralOff = (gameObject.GetInstanceID() & 1) == 0 ? 1f : -1f;
            }
            else
            {
                Vector2 toNorm = toOther / dist;
                ahead      = Vector2.Dot(moveDir, toNorm);
                lateralOff = Vector2.Dot(lateral, toNorm);
            }

            // Only avoid heroes that are in our forward hemisphere.
            if (ahead <= 0f) continue;

            // Steer opposite to the side the other hero is on.
            // If |lateralOff| is tiny (nearly head-on), fall back to instance-ID split.
            float steerSign;
            if (Mathf.Abs(lateralOff) < 0.08f)
                steerSign = (gameObject.GetInstanceID() & 1) == 0 ? 1f : -1f;
            else
                steerSign = lateralOff > 0f ? -1f : 1f;

            float proximity = 1f - Mathf.Clamp01(dist / AvoidRadius);
            steer += lateral * steerSign * proximity * ahead * AvoidStrength;
        }

        return steer;
    }

    /// <summary>
    /// Strips redundant intermediate waypoints from the raw A* path using LOS tests.
    /// Only direction-change nodes (corners) and the endpoint survive, turning a dense
    /// staircase of grid nodes into a compact list of straight-line segments.
    /// </summary>
    private List<PathNode> SmoothenPath(List<PathNode> raw)
    {
        if (raw == null || raw.Count <= 2) return raw;

        var result = new List<PathNode>();
        result.Add(raw[0]);

        int i = 0;
        while (i < raw.Count - 1)
        {
            // Search backwards from the end of the path to find the furthest node
            // reachable in a straight line from raw[i], then jump straight to it.
            int furthest = i + 1;
            for (int j = raw.Count - 1; j > i + 1; j--)
            {
                if (raw[j] != null && raw[j] &&
                    HasClearLine(raw[i].transform.position, raw[j].transform.position))
                {
                    furthest = j;
                    break;
                }
            }
            result.Add(raw[furthest]);
            i = furthest;
        }

        return result;
    }

    /// <summary>
    /// Returns true when a straight world-space line between <paramref name="from"/> and
    /// <paramref name="to"/> passes only over walkable floor tiles (no walls in the way).
    /// Samples the line every 0.4 units and queries the grid directly, which works
    /// correctly for dungeons where walls are represented as absent tiles rather than
    /// solid colliders.
    /// </summary>
    private bool HasClearLine(Vector3 from, Vector3 to)
    {
        var gridGen = GridGenerator.Instance;
        if (gridGen == null) return false;

        float dist  = Vector2.Distance(from, to);
        int   steps = Mathf.Max(2, Mathf.CeilToInt(dist / 0.4f));

        for (int i = 1; i < steps; i++)
        {
            Vector3  sample = Vector3.Lerp(from, to, (float)i / steps);
            PathNode node   = gridGen.GetNodeAtWorldPosition(sample);
            if (node == null || !node.isWalkable) return false;
        }
        return true;
    }

    /// <summary>
    /// Returns the first ENEMY collider standing on <paramref name="node"/>, or null if clear.
    ///
    /// Friendly heroes are intentionally NOT checked here — party members pass through
    /// each other for pathfinding purposes (SeparationBehavior handles physical spacing).
    /// This lets melee followers path through the leader to reach an enemy in a corridor
    /// instead of deadlocking behind them indefinitely.
    /// </summary>
    private Collider2D GetBlockerCollider(PathNode node)
    {
        if (node == null || !node) return null;

        const float checkRadius = 0.35f;
        int mask = LayerMask.GetMask("Enemy");

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            node.transform.position, checkRadius, mask);

        foreach (var col in hits)
        {
            if (col == null) continue;
            if (col.gameObject != gameObject) return col;
        }
        return null;
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