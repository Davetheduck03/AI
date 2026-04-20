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
///
/// CORNER-WEDGE FIX: both separation and yield now test the candidate landing
/// position against wall-adjacent tiles (CountWallNeighbors >= 3) in addition to
/// the walkability check. This prevents heroes from being nudged into tight
/// inner-corner cells where A* cannot path them back out cleanly, which was the
/// primary cause of the "stuck in a corner" bug.
/// </summary>
public class SeparationBehavior : MonoBehaviour
{
	// ── Separation ────────────────────────────────────────────────────────────

	[Tooltip("Distance (world units) at which separation starts.")]
	[SerializeField] private float separationRadius = 1.0f;

	[Tooltip("Maximum push speed when the hero is fully stopped.")]
	[SerializeField] private float separationSpeed = 5.0f;

	private const float MovingStrengthScale = 0.15f;

	// ── Yielding ──────────────────────────────────────────────────────────────

	[Tooltip("Radius within which this hero detects an approaching higher-priority mover and steps aside.")]
	[SerializeField] private float yieldRadius = 1.5f;

	[Tooltip("Speed at which this hero sidesteps when yielding to a higher-priority hero.")]
	[SerializeField] private float yieldSpeed = 3.5f;

	// ── Wall-adjacent rejection ───────────────────────────────────────────────
	// A PathNode with this many or more non-walkable neighbours is considered
	// wall-adjacent and should not be used as a push/yield destination.
	// 3+ wall neighbours = inner corner tile = dead-end for A*.
	private const int WallAdjacentThreshold = 3;

	// ── Internal refs ─────────────────────────────────────────────────────────

	private int _heroLayerMask;
	private UnitPathFollower _pathFollower;

	private void Awake()
	{
		_heroLayerMask = 1 << LayerMask.NameToLayer("Player");
		_pathFollower = GetComponent<UnitPathFollower>();
	}

	private void Update()
	{
		bool inCombat = TeamBlackboard.Instance?.Get<Transform>("leaderCombatTarget") != null;
		bool moving = _pathFollower != null && _pathFollower.IsFollowingPath;

		// ── Separation ────────────────────────────────────────────────────────
		{
			Collider2D[] nearby = Physics2D.OverlapCircleAll(
				transform.position, separationRadius, _heroLayerMask);

			Vector2 push = Vector2.zero;

			foreach (Collider2D col in nearby)
			{
				if (col == null || col.gameObject == gameObject) continue;

				Vector2 away = (Vector2)(transform.position - col.transform.position);
				float dist = away.magnitude;

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
				float scale = moving ? MovingStrengthScale : (inCombat ? 0f : 1f);
				if (scale > 0f)
				{
					Vector3 delta = (Vector3)(push.normalized * separationSpeed * scale * Time.deltaTime);
					Vector3 newPos = transform.position + delta;

					if (IsWalkableAndOpen(newPos))
						transform.position = newPos;
				}
			}
		}

		// ── Yield to higher-priority movers ───────────────────────────────────
		if (!inCombat && !moving)
		{
			Collider2D[] yieldArea = Physics2D.OverlapCircleAll(
				transform.position, yieldRadius, _heroLayerMask);

			Vector2 yieldDir = Vector2.zero;
			int mySlot = GetHeroPriority(transform);

			foreach (Collider2D col in yieldArea)
			{
				if (col == null || col.gameObject == gameObject) continue;

				int theirSlot = GetHeroPriority(col.transform);
				if (theirSlot >= mySlot) continue;

				var otherPF = col.GetComponent<UnitPathFollower>();
				if (otherPF == null || !otherPF.IsFollowingPath) continue;

				Vector2 toThem = (Vector2)col.transform.position - (Vector2)transform.position;
				float dist = toThem.magnitude;
				if (dist < 0.001f) continue;

				Vector2 toNorm = toThem / dist;
				Vector2 perp = new Vector2(-toNorm.y, toNorm.x);
				float side = ((gameObject.GetInstanceID() ^ col.gameObject.GetInstanceID()) & 1) == 0
								 ? 1f : -1f;

				float proximity = 1f - Mathf.Clamp01(dist / yieldRadius);
				yieldDir += perp * side * proximity;
			}

			if (yieldDir.sqrMagnitude > 0.001f)
			{
				Vector3 newPos = transform.position +
								 (Vector3)(yieldDir.normalized * yieldSpeed * Time.deltaTime);

				if (IsWalkableAndOpen(newPos))
					transform.position = newPos;
			}
		}
	}

	/// <summary>
	/// Returns true only when <paramref name="worldPos"/> lies on a walkable tile
	/// AND that tile is not a wall-adjacent inner-corner (3+ wall neighbours).
	/// Rejecting wall-adjacent inner corners prevents heroes from being nudged into
	/// the tight cells at the inside of corridor bends, which A* cannot reliably
	/// path out of and which causes the "stuck in a corner" freeze.
	/// </summary>
	private static bool IsWalkableAndOpen(Vector3 worldPos)
	{
		var grid = GridGenerator.Instance;
		if (grid == null) return false;

		var node = grid.GetNodeAtWorldPosition(worldPos);
		if (node == null || !node.isWalkable) return false;

		// Count non-walkable (wall) neighbours — mirrors the helper in Astar.cs.
		int walls = 0;
		foreach (var n in node.neighbors)
			if (n == null || !n.isWalkable) walls++;

		return walls < WallAdjacentThreshold;
	}

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