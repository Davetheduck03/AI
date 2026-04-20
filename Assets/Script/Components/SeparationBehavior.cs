using UnityEngine;

/// <summary>
/// Three behaviours in one component:
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
/// WALL / CORNER REPULSION — pushes heroes away from immediately adjacent wall
///   tiles at all times.  Strength scales with context: full when idle, 20–25 %
///   while moving or in combat, 15 % when both.  Always-on repulsion means heroes
///   naturally drift away from concave corners during path-following and combat,
///   not just when standing still.  A special "corner escape" mode kicks in when
///   the hero is already on an isInnerCorner tile: the destination check is relaxed
///   to just IsWalkable (not IsWalkableAndOpen) so the push can actually fire and
///   the hero is not permanently trapped.
///
/// CORNER-WEDGE FIX: separation and yield both test the candidate landing
/// position against the PathNode.isInnerCorner flag in addition to the walkability
/// check, so heroes are never nudged into inner-corner cells in the first place.
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

	// ── Wall repulsion ────────────────────────────────────────────────────────

	// Maximum speed of the wall-repulsion push (world units per second).
	// Deliberately lower than separationSpeed so it doesn't overpower deliberate
	// path-following, but high enough to visibly drift heroes away from walls.
	private const float WallRepulsionSpeed = 2.0f;

	// Only push away from wall tiles that are within this many grid cells.
	// 1 cell = directly adjacent tile; 1.5 catches diagonal wall adjacency.
	// Using the grid cell size (1 u) as the probe distance.
	private const float WallProbeDistance  = 1.0f;

	// ── Wall-adjacent rejection ───────────────────────────────────────────────
	// A PathNode with isInnerCorner == true should not be used as a push/yield
	// destination because it is a dead-end for A* escape paths.
	// Kept for the IsWalkableAndOpen check; the threshold is still useful as a
	// secondary guard (3+ cardinal wall neighbors = very tight cell).
	private const int WallAdjacentThreshold = 3;

	// ── Yield commitment ─────────────────────────────────────────────────────
	// Once this hero commits to a yield direction, maintain it for
	// YieldCommitDuration seconds before reconsidering.  Without this, the yield
	// direction recalculates every frame.  When the approaching leader oscillates
	// (e.g. avoidance steering nudges it left then right), the perpendicular
	// direction flips sign each frame and the yielding follower spins in place.
	private Vector2 _committedYieldDir  = Vector2.zero;
	private float   _yieldCommitExpires = 0f;
	private const float YieldCommitDuration = 0.25f;

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
			// If we're still within a committed yield window, reuse the last direction
			// rather than recomputing — this prevents spinning when the approaching
			// hero's exact angle oscillates due to its own avoidance steering.
			Vector2 yieldDir;

			if (Time.time < _yieldCommitExpires && _committedYieldDir.sqrMagnitude > 0.001f)
			{
				// Committed direction: just keep moving in the same direction.
				yieldDir = _committedYieldDir;
			}
			else
			{
				// Recompute a fresh yield direction from nearby higher-priority movers.
				Collider2D[] yieldArea = Physics2D.OverlapCircleAll(
					transform.position, yieldRadius, _heroLayerMask);

				yieldDir = Vector2.zero;
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
					Vector2 perp   = new Vector2(-toNorm.y, toNorm.x);
					float side     = ((gameObject.GetInstanceID() ^ col.gameObject.GetInstanceID()) & 1) == 0
									     ? 1f : -1f;

					float proximity = 1f - Mathf.Clamp01(dist / yieldRadius);
					yieldDir += perp * side * proximity;
				}

				// Commit to this direction for YieldCommitDuration so a transient
				// change in the approaching hero's angle can't immediately flip us.
				if (yieldDir.sqrMagnitude > 0.001f)
				{
					_committedYieldDir  = yieldDir.normalized;
					_yieldCommitExpires = Time.time + YieldCommitDuration;
				}
				else
				{
					// No one to yield to — clear the commitment.
					_committedYieldDir  = Vector2.zero;
					_yieldCommitExpires = 0f;
				}
			}

			if (yieldDir.sqrMagnitude > 0.001f)
			{
				Vector3 newPos = transform.position +
								 (Vector3)(yieldDir.normalized * yieldSpeed * Time.deltaTime);

				if (IsWalkableAndOpen(newPos))
					transform.position = newPos;
			}
		}

		// ── Wall / corner repulsion ─────────────────────────────────────────────
		// Always active.  Strength scales down while moving or in combat so it
		// never fights deliberate path-following or melee attack positioning, but
		// it is never zero — heroes should drift away from corners at all times.
		//
		// Crucially this now fires during movement and combat, not just while
		// stopped-and-idle.  That is the root cause of heroes getting wedged:
		// path-following drops them flush against a concave corner and then the
		// repulsion that could rescue them was dormant.
		//
		// CORNER ESCAPE: when the hero is already sitting on an isInnerCorner tile
		// we relax the destination check from IsWalkableAndOpen → IsWalkable.
		// Without this relaxation the escape vector is silently rejected every frame
		// because the preferred escape tile is itself an adjacent inner-corner, and
		// the hero can never leave.
		{
			var grid = GridGenerator.Instance;
			if (grid != null)
			{
				// Strength scaling:
				//   Stopped, no combat → full strength (was the only case before)
				//   Moving             → 20 % — gently drift away without fighting A*
				//   In combat          → 25 % — keep heroes off walls while attacking
				//   Moving + combat    → 15 % — minimal, don't disrupt kiting
				float wallScale = 1.0f;
				if (moving && inCombat) wallScale = 0.15f;
				else if (moving)        wallScale = 0.20f;
				else if (inCombat)      wallScale = 0.25f;

				Vector2 wallPush = Vector2.zero;

				// All 8 probe directions, normalized so diagonal probes weigh the
				// same as cardinal ones.
				Vector2[] probeDirs = new Vector2[]
				{
					Vector2.up,    Vector2.down,
					Vector2.left,  Vector2.right,
					new Vector2( 1f,  1f).normalized,
					new Vector2( 1f, -1f).normalized,
					new Vector2(-1f,  1f).normalized,
					new Vector2(-1f, -1f).normalized,
				};

				foreach (Vector2 dir in probeDirs)
				{
					Vector3 probe    = transform.position + (Vector3)(dir * WallProbeDistance);
					var     neighbor = grid.GetNodeAtWorldPosition(probe);

					if (neighbor == null || !neighbor.isWalkable)
						wallPush += -dir;
				}

				if (wallPush.sqrMagnitude > 0.001f)
				{
					Vector3 newPos = transform.position +
					                 (Vector3)(wallPush.normalized * WallRepulsionSpeed * wallScale * Time.deltaTime);

					// If already in a corner, relax the open-tile guard so the escape
					// push can actually fire.  We still require the target to be walkable
					// (no pushing into walls), but we drop the isInnerCorner/wallNeighbor
					// rejection that would otherwise block the only escape direction.
					var currentNode = grid.GetNodeAtWorldPosition(transform.position);
					bool inCorner   = currentNode != null && currentNode.isInnerCorner;

					bool canMove = inCorner ? IsWalkable(newPos) : IsWalkableAndOpen(newPos);
					if (canMove)
						transform.position = newPos;
				}
			}
		}
	}

	/// <summary>
	/// Returns true only when <paramref name="worldPos"/> lies on a walkable tile
	/// that is not an inner-corner cell.
	///
	/// Inner corners (PathNode.isInnerCorner) are tiles where two orthogonally-
	/// adjacent cardinal wall tiles form an L-shape.  Heroes pushed into them tend
	/// to get physically wedged and cannot be re-pathed out cleanly.  The flag is
	/// pre-computed by GridGenerator.MarkCornerTiles after every grid rebuild, so
	/// this check is O(1) with no neighbor iteration.
	///
	/// The legacy WallAdjacentThreshold (3+ cardinal wall neighbors) is kept as a
	/// secondary guard for any node that slips through the primary flag.
	/// </summary>
	private static bool IsWalkableAndOpen(Vector3 worldPos)
	{
		var grid = GridGenerator.Instance;
		if (grid == null) return false;

		var node = grid.GetNodeAtWorldPosition(worldPos);
		if (node == null || !node.isWalkable) return false;

		// Fast path: reject inner-corner tiles using the pre-computed flag.
		if (node.isInnerCorner) return false;

		// Secondary guard: tiles with 3+ cardinal wall neighbors are very tight
		// even if they don't qualify as a strict L-corner (e.g. a 1-tile dead-end).
		return node.wallNeighborCount < WallAdjacentThreshold;
	}

	/// <summary>
	/// Minimal walkability check used when the hero is already inside an inner-corner
	/// and needs to escape.  Only rejects actual wall tiles — does not reject other
	/// inner-corner or wall-adjacent tiles because any walkable tile is better than
	/// staying wedged.
	/// </summary>
	private static bool IsWalkable(Vector3 worldPos)
	{
		var grid = GridGenerator.Instance;
		if (grid == null) return false;

		var node = grid.GetNodeAtWorldPosition(worldPos);
		return node != null && node.isWalkable;
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