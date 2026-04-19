using UnityEngine;

/// <summary>
/// Two behaviours in one component:
///
/// SEPARATION — pushes stopped heroes apart when they overlap on the same tile.
///   While a path is running, strength scales to near-zero so it doesn't fight
///   the path-follower's avoidance steering.  During combat it is zeroed entirely
///   so melee heroes hold their attack position without being jostled out of range.
///
/// YIELDING — when a stopped hero detects an actively moving, higher-priority hero
///   nearby, it sidesteps perpendicular to clear the lane.
///   Priority: slot 0 (leader) > slot 1 > slot 2 > slot 3.
///   Heroes that are themselves moving rely on UnitPathFollower's avoidance
///   steering, so yielding is skipped while IsFollowingPath is true.
///   Yielding is also suppressed during combat so attack positions stay stable.
/// </summary>
public class SeparationBehavior : MonoBehaviour
{
    // ── Separation ────────────────────────────────────────────────────────────

    [Tooltip("Distance (world units) at which separation starts.")]
    [SerializeField] private float separationRadius = 1.0f;

    [Tooltip("Maximum push speed when the hero is fully stopped.")]
    [SerializeField] private float separationSpeed = 5.0f;

    // Strength multiplier applied while a path is actively running.
    // Keep small — avoidance steering handles the dynamic case.
    private const float MovingStrengthScale = 0.15f;

    // ── Yielding ──────────────────────────────────────────────────────────────

    [Tooltip("Radius within which this hero detects an approaching higher-priority mover and steps aside.")]
    [SerializeField] private float yieldRadius = 1.5f;

    [Tooltip("Speed at which this hero sidesteps when yielding to a higher-priority hero.")]
    [SerializeField] private float yieldSpeed = 3.5f;

    // ── Internal refs ─────────────────────────────────────────────────────────

    private int              _heroLayerMask;
    private UnitPathFollower _pathFollower;

    private void Awake()
    {
        _heroLayerMask = 1 << LayerMask.NameToLayer("Player");
        _pathFollower  = GetComponent<UnitPathFollower>();
    }

    private void Update()
    {
        bool inCombat = TeamBlackboard.Instance?.Get<Transform>("leaderCombatTarget") != null;
        bool moving   = _pathFollower != null && _pathFollower.IsFollowingPath;

        // ── Separation ────────────────────────────────────────────────────────
        {
            Collider2D[] nearby = Physics2D.OverlapCircleAll(
                transform.position, separationRadius, _heroLayerMask);

            Vector2 push = Vector2.zero;

            foreach (Collider2D col in nearby)
            {
                if (col == null || col.gameObject == gameObject) continue;

                Vector2 away = (Vector2)(transform.position - col.transform.position);
                float   dist = away.magnitude;

                if (dist < 0.001f)
                {
                    float angle = (gameObject.GetInstanceID() & 0xFF) * (Mathf.PI * 2f / 256f);
                    away = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    dist = 0.001f;
                }

                float strength = 1f - Mathf.Clamp01(dist / separationRadius);
                push += away.normalized * strength;
            }

            if (push.sqrMagnitude > 0.001f)
            {
                // Disable during combat — heroes must hold their attack positions.
                // During movement, scale way down so separation doesn't oppose the path.
                float scale = moving ? MovingStrengthScale : (inCombat ? 0f : 1f);
                if (scale > 0f)
                {
                    Vector3 delta  = (Vector3)(push.normalized * separationSpeed * scale * Time.deltaTime);
                    Vector3 newPos = transform.position + delta;
                    // Don't push heroes into walls — only apply the separation if the
                    // target position lies on a walkable tile. This prevents heroes from
                    // being nudged slightly into wall tiles in narrow corridors, which
                    // causes A* to spend every call snapping the start position back out.
                    var landingNode = GridGenerator.Instance?.GetNodeAtWorldPosition(newPos);
                    if (landingNode != null && landingNode.isWalkable)
                        transform.position = newPos;
                }
            }
        }

        // ── Yield to higher-priority movers ───────────────────────────────────
        // A stopped hero detects any actively moving hero with a higher slot
        // priority and sidesteps perpendicular to clear the lane for them.
        // Skipped during combat (hold attack position) and while this hero is
        // already moving (avoidance steering handles the in-motion case).
        if (!inCombat && !moving)
        {
            Collider2D[] yieldArea = Physics2D.OverlapCircleAll(
                transform.position, yieldRadius, _heroLayerMask);

            Vector2 yieldDir = Vector2.zero;
            int     mySlot   = GetHeroPriority(transform);

            foreach (Collider2D col in yieldArea)
            {
                if (col == null || col.gameObject == gameObject) continue;

                // Only yield to heroes with a higher priority (lower slot number).
                int theirSlot = GetHeroPriority(col.transform);
                if (theirSlot >= mySlot) continue;

                // Only yield to heroes that are currently following a path.
                var otherPF = col.GetComponent<UnitPathFollower>();
                if (otherPF == null || !otherPF.IsFollowingPath) continue;

                // Sidestep perpendicular to the line toward the approaching hero.
                // The side is chosen by XOR-ing both instance IDs so the result is
                // deterministic and identical regardless of which hero evaluates first,
                // preventing the two heroes from choosing opposite sides and colliding.
                Vector2 toThem = (Vector2)col.transform.position - (Vector2)transform.position;
                float   dist   = toThem.magnitude;
                if (dist < 0.001f) continue;

                Vector2 toNorm = toThem / dist;
                Vector2 perp   = new Vector2(-toNorm.y, toNorm.x); // left-perpendicular
                float   side   = ((gameObject.GetInstanceID() ^ col.gameObject.GetInstanceID()) & 1) == 0
                                 ? 1f : -1f;

                // Scale by proximity so the push grows as the other hero closes in.
                float proximity = 1f - Mathf.Clamp01(dist / yieldRadius);
                yieldDir += perp * side * proximity;
            }

            if (yieldDir.sqrMagnitude > 0.001f)
            {
                // Mirror the separation block's walkability guard: never step
                // the yielding hero into a wall tile.  Without this, a hero
                // pressed toward a wall in a narrow corridor gets nudged into
                // a non-walkable cell — A* then snaps the start position back
                // on every call, producing a visible stutter.
                Vector3 newPos = transform.position +
                                 (Vector3)(yieldDir.normalized * yieldSpeed * Time.deltaTime);
                var landingNode = GridGenerator.Instance?.GetNodeAtWorldPosition(newPos);
                if (landingNode != null && landingNode.isWalkable)
                    transform.position = newPos;
            }
        }
    }

    /// <summary>
    /// Returns the formation slot index for <paramref name="hero"/>.
    /// Lower number = higher priority (slot 0 = leader = highest).
    /// Heroes not registered in FormationManager get a large fallback value so
    /// they never outrank registered members.
    /// </summary>
    private static int GetHeroPriority(Transform hero)
    {
        var fm = FormationManager.Instance;
        if (fm != null)
        {
            int slot = fm.GetSlot(hero);
            if (slot >= 0) return slot;
        }
        return 1000 + (Mathf.Abs(hero.GetInstanceID()) & 0xFF);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, yieldRadius);
    }
#endif
}