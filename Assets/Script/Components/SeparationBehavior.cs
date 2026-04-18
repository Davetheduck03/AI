using UnityEngine;

/// <summary>
/// Pushes stopped heroes apart when they overlap on the same tile.
///
/// While a path is actively running, UnitPathFollower's built-in avoidance
/// steering handles lateral lane-changing — radial separation during movement
/// directly opposes forward velocity and causes heroes to stall.  This component
/// therefore scales its strength down to near-zero while a path is running,
/// and back up to full strength once the hero is stationary.
/// </summary>
public class SeparationBehavior : MonoBehaviour
{
    [Tooltip("Distance (world units) at which separation starts.")]
    [SerializeField] private float separationRadius = 1.0f;

    [Tooltip("Maximum push speed when the hero is fully stopped.")]
    [SerializeField] private float separationSpeed = 5.0f;

    // Strength multiplier applied while a path is actively running.
    // Keep this small — avoidance steering handles the dynamic case.
    private const float MovingStrengthScale = 0.15f;

    private int              _heroLayerMask;
    private UnitPathFollower _pathFollower;

    private void Awake()
    {
        _heroLayerMask = 1 << LayerMask.NameToLayer("Player");
        _pathFollower  = GetComponent<UnitPathFollower>();
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
                float angle = (gameObject.GetInstanceID() & 0xFF) * (Mathf.PI * 2f / 256f);
                away = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                dist = 0.001f;
            }

            float strength = 1f - Mathf.Clamp01(dist / separationRadius);
            push += away.normalized * strength;
        }

        if (push.sqrMagnitude > 0.001f)
        {
            // Scale down while moving so we don't fight the path-follower.
            bool  moving = _pathFollower != null && _pathFollower.IsFollowingPath;
            float scale  = moving ? MovingStrengthScale : 1f;

            transform.position +=
                (Vector3)(push.normalized * separationSpeed * scale * Time.deltaTime);
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
