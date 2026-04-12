using UnityEngine;

/// <summary>
/// Nudges this hero away from other nearby heroes when they enter its personal space.
///
/// Works in transform-space (no Rigidbody2D) to stay consistent with
/// UnitPathFollower's coroutine-based movement model.  The nudge is additive:
/// Update() runs alongside the path-follower coroutine each frame, so the hero
/// drifts apart from neighbours while still following its A* path.
///
/// This also fires when FollowLeader has stopped the path (unit is "in position")
/// so heroes don't freeze on top of each other in corridors.
/// </summary>
public class SeparationBehavior : MonoBehaviour
{
    [Tooltip("Distance (world units) at which separation starts.  " +
             "Should be larger than UnitPathFollower's blocker check radius (0.35).")]
    [SerializeField] private float separationRadius = 0.65f;

    [Tooltip("Maximum nudge speed in world units per second.  " +
             "Scales linearly to zero at the edge of separationRadius.")]
    [SerializeField] private float separationSpeed = 2.5f;

    // Cached so we don't call NameToLayer every Update.
    private int _heroLayerMask;

    private void Awake()
    {
        _heroLayerMask = 1 << LayerMask.NameToLayer("Player");
    }

    private void Update()
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
                // Exact overlap: use a deterministic angle derived from the instance
                // ID so two fully-overlapping heroes push in opposite directions and
                // don't cancel each other out.
                float angle = (gameObject.GetInstanceID() & 0xFF) * (Mathf.PI * 2f / 256f);
                away = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                dist = 0.001f;
            }

            // Linear fall-off: full strength at zero overlap, zero at the radius edge.
            float strength = 1f - Mathf.Clamp01(dist / separationRadius);
            push += away.normalized * strength;
        }

        if (push.sqrMagnitude > 0.001f)
        {
            transform.position +=
                (Vector3)(push.normalized * separationSpeed * Time.deltaTime);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);
    }
#endif
}
