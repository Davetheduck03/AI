// KiteAndAttack.cs
using UnityEngine;

/// <summary>
/// Kiting combat node for ranged units.
///
/// States:
///   CLOSING    — enemy beyond attackRange; move toward them.
///   STRAFING   — enemy in the sweet-spot band; hold and shoot, side-step periodically.
///   RETREATING — enemy inside kiteDistance; back away while still firing.
///
/// IMPORTANT: the enemy Transform is stored in a private field (_enemy), NOT in
/// bb["target"].  Writing the kite movement target into bb["target"] was the
/// original bug that caused the node to lose track of the enemy every frame.
/// </summary>
public class KiteAndAttack : Node
{
	// ── Tuning ───────────────────────────────────────────────────────────────

	private readonly float kiteDistance;

	private const float RetreatTriggerMargin = 0.3f;   // deadzone inside kiteDistance
	private const float CloseInMargin = 0.5f;   // deadzone outside attackRange
	private const float StrafeInterval = 1.8f;   // seconds between side-steps
	private const float StrafeDistance = 2.5f;   // world units per side-step
	private const float StuckDistanceThreshold = 0.25f;
	private const float MovementCheckInterval = 2.0f;

	// How often to re-trigger movement while closing/retreating (seconds).
	// Calling OnTriggerMove every frame would spam A* — throttle it.
	private const float MoveRetriggerInterval = 0.5f;

	// Give up chasing if we haven't closed in after this many seconds.
	// Prevents the unit from locking onto an unreachable enemy indefinitely.
	private const float ClosingTimeout     = 8f;
	private float       _closingStartTime  = float.MaxValue;

	// After timing out on an enemy, ignore that specific enemy for this long so
	// the behaviour tree can fall through to FollowLeader and catch up with the team.
	// Without this, FindNearestRevealedEnemy re-targets the same enemy the very next
	// tick and the archer is trapped in an 8-second chase loop forever.
	private const float GaveUpCooldown = 12f;
	private Transform   _gaveUpEnemy   = null;
	private float       _gaveUpTime    = float.MinValue;

	// ── State ─────────────────────────────────────────────────────────────────

	private enum CombatState { Closing, Strafing, Retreating }
	private CombatState _state = CombatState.Closing;

	private readonly LayerMask _targetLayer;
	private float _lastAttackTime = 0f;
	private float _nextStrafeTime = 0f;
	private float _nextMoveRetrigger = 0f;
	private int _strafeDirection = 1;

	// Enemy is stored here — NOT in the blackboard — so movement target
	// writes can't accidentally overwrite it.
	private Transform _enemy = null;

	// Reusable GO for movement targets (avoid per-frame allocation).
	private GameObject _moveTargetGO = null;

	// Stuck detection for retreat
	private Vector3 _lastCheckedPos = Vector3.zero;
	private float _nextStuckCheckAt = 0f;

	// ── Constructor ───────────────────────────────────────────────────────────

	private readonly LayerMask _wallLayers;

	public KiteAndAttack(Blackboard bb, LayerMask targetLayer, float kiteDistance = 3.5f, LayerMask wallLayers = default)
		: base(bb)
	{
		this.kiteDistance = kiteDistance;
		_targetLayer = targetLayer;
		_wallLayers = wallLayers;
	}

	// ── Evaluate ──────────────────────────────────────────────────────────────

	public override NodeState Evaluate()
	{
		Transform self = bb.Get<Transform>("self");

		// Read the enemy from the blackboard ONLY on the first call or when
		// the blackboard has a fresh enemy (set by FindNearestRevealedEnemy).
		Transform bbEnemy = bb.Get<Transform>("target");
		if (bbEnemy != null && bbEnemy != _moveTargetGO?.transform)
		{
			// If we recently timed out trying to reach this specific enemy, ignore it
			// for the cooldown period so the BT can fall through to FollowLeader.
			if (bbEnemy == _gaveUpEnemy && Time.time - _gaveUpTime < GaveUpCooldown)
				return NodeState.Failure;

			if (bbEnemy != _enemy)          // brand new target — reset closing timer
				_closingStartTime = Time.time;
			_enemy = bbEnemy;               // real enemy — accept it
		}

		if (self == null || _enemy == null || _enemy.gameObject == null)
		{
			Cleanup(self);
			return NodeState.Failure;
		}

		if (!self.TryGetComponent<DamageComponent>(out var damageComp))
			return NodeState.Failure;

		float effectiveAttackRange = damageComp.AttackRange;
		float effectiveKiteRange = Mathf.Min(kiteDistance, effectiveAttackRange - 0.3f);
		float dist = Vector2.Distance(self.position, _enemy.position);

		// ── State transition ──────────────────────────────────────────────────
		CombatState newState;
		if (dist < effectiveKiteRange - RetreatTriggerMargin)
			newState = CombatState.Retreating;
		else if (dist > effectiveAttackRange + CloseInMargin)
			newState = CombatState.Closing;
		else
			newState = CombatState.Strafing;

		if (newState != _state)
		{
			_state = newState;
			_nextMoveRetrigger = 0f;   // force immediate move on state change
			if (_state == CombatState.Closing)
				_closingStartTime = Time.time;  // start timeout when we enter Closing
			if (_state == CombatState.Strafing || _state == CombatState.Retreating)
				_closingStartTime = float.MaxValue;  // reached the enemy, cancel timer
			if (_state == CombatState.Retreating)
			{
				_lastCheckedPos = self.position;
				_nextStuckCheckAt = Time.time + MovementCheckInterval;
			}
		}

		// ── Movement ──────────────────────────────────────────────────────────
		switch (_state)
		{
			case CombatState.Closing:
				// Give up if we've been closing for too long without reaching attack range.
				// This happens when the enemy is behind a wall or too far to path to.
				if (Time.time - _closingStartTime > ClosingTimeout)
				{
					Debug.Log($"[KiteAndAttack] {self.name} timed out closing on {_enemy?.name} — giving up");
					// Blacklist this enemy so FindNearestRevealedEnemy can't immediately
					// re-target it next tick, which would restart the chase loop.
					_gaveUpEnemy = _enemy;
					_gaveUpTime  = Time.time;
					Cleanup(self);
					return NodeState.Failure;
				}
				if (Time.time >= _nextMoveRetrigger)
				{
					TriggerMove(self, _enemy.position);
					_nextMoveRetrigger = Time.time + MoveRetriggerInterval;
				}
				break;

			case CombatState.Retreating:
				if (!TryRetreat(self))
					StopMovement(self);
				break;

			case CombatState.Strafing:
				TryStrafe(self);
				break;
		}

		// ── Attack ────────────────────────────────────────────────────────────
		// Check line-of-sight before firing — walls block ranged attacks.
		bool hasLOS = _wallLayers == 0 ||
		              VisionUtilities.HasLineOfSight(self.position, _enemy.position, _wallLayers);

		// No LOS → force Closing so the archer moves around the obstacle.
		if (!hasLOS && _state != CombatState.Closing)
		{
			_state = CombatState.Closing;
			_nextMoveRetrigger = 0f;
		}

		if (hasLOS && dist <= effectiveAttackRange &&
			Time.time - _lastAttackTime >= damageComp.AttackCooldown)
		{
			// Cache name before damage (the GO may be destroyed inside TryDealDamage)
			string enemyName = _enemy.name;

			// AoE units (e.g. Mage) blast everything around them; single-target units hit only the enemy.
			if (damageComp.IsAoE)
				damageComp.TryDealDamage(_enemy.gameObject, self.position, _targetLayer);
			else
				damageComp.TryDealDamage(_enemy.gameObject);

			_lastAttackTime = Time.time;
			Debug.Log($"[KiteAndAttack] {self.name} hit {enemyName} " +
					  $"dist={dist:F2} state={_state} aoe={damageComp.IsAoE}");

			// If the enemy died from this hit, clean up immediately so the retreat/
			// strafe movement coroutine stops and bb["target"] is cleared.
			// Without this the path-follower keeps running the old retreat path and
			// the hero visibly stands still for up to 3 s after clearing the area.
			if (_enemy == null || _enemy.gameObject == null)
				Cleanup(self);

			return NodeState.Success;
		}

		return NodeState.Running;
	}

	// ── Movement helpers ──────────────────────────────────────────────────────

	private bool TryRetreat(Transform self)
	{
		// Stuck check: if we haven't actually moved since the last interval, force an
		// immediate re-evaluation so we try an alternative direction this very frame.
		if (Time.time >= _nextStuckCheckAt)
		{
			float moved = Vector3.Distance(self.position, _lastCheckedPos);
			_lastCheckedPos = self.position;
			_nextStuckCheckAt = Time.time + MovementCheckInterval;

			if (moved < StuckDistanceThreshold)
			{
				Debug.Log($"[KiteAndAttack] {self.name} stuck retreating — trying alternative directions.");
				_nextMoveRetrigger = 0f;   // force re-evaluation now, don't just give up
			}
		}

		if (Time.time < _nextMoveRetrigger) return true;   // already moving, wait

		Vector3 awayDir   = (self.position - _enemy.position).normalized;
		Vector3 leftPerp  = new Vector3(-awayDir.y,  awayDir.x, 0f);
		Vector3 rightPerp = new Vector3( awayDir.y, -awayDir.x, 0f);

		float stepBack = Mathf.Max(
			kiteDistance - Vector2.Distance(self.position, _enemy.position) + RetreatTriggerMargin + 1.0f,
			1.5f);

		// Candidate escape directions in priority order:
		//   1. Straight back          (ideal kite)
		//   2. Back-left diagonal     (slide left while backing)
		//   3. Back-right diagonal    (slide right while backing)
		//   4. Pure left              (corridor sidestep)
		//   5. Pure right             (corridor sidestep)
		Vector3[] candidates =
		{
			awayDir,
			(awayDir + leftPerp).normalized,
			(awayDir + rightPerp).normalized,
			leftPerp,
			rightPerp,
		};

		foreach (var dir in candidates)
		{
			Vector3 dest = self.position + dir * stepBack;
			// GetNodeAtWorldPosition returns null for walls / outside the map, so a
			// non-null result is a sufficient walkability check.
			if (GridGenerator.Instance?.GetNodeAtWorldPosition(dest) != null)
			{
				TriggerMove(self, dest);
				_nextMoveRetrigger = Time.time + MoveRetriggerInterval;
				return true;
			}
		}

		// Fully cornered — every direction leads into a wall, just hold fire.
		Debug.Log($"[KiteAndAttack] {self.name} fully cornered — holding position.");
		return false;
	}

	private void TryStrafe(Transform self)
	{
		if (Time.time < _nextStrafeTime)
		{
			StopMovement(self);
			return;
		}

		_nextStrafeTime = Time.time + StrafeInterval;
		_strafeDirection = -_strafeDirection;

		Vector3 toEnemy = (_enemy.position - self.position).normalized;
		Vector3 perpDir = new Vector3(-toEnemy.y, toEnemy.x, 0f) * _strafeDirection;
		TriggerMove(self, self.position + perpDir * StrafeDistance);
	}

	/// <summary>
	/// Moves the unit toward <paramref name="worldPos"/> via the pathfinding system.
	/// Uses a reusable hidden GameObject as the movement target so the pathfinder
	/// has a Transform to navigate to — this GO is intentionally never stored in
	/// bb["target"] to avoid clobbering the enemy reference.
	/// </summary>
	private void TriggerMove(Transform self, Vector3 worldPos)
	{
		if (_moveTargetGO == null)
			_moveTargetGO = new GameObject("_KiteTarget");

		_moveTargetGO.transform.position = worldPos;

		// Do NOT write _moveTargetGO into bb["target"] — that would replace
		// the enemy reference and break the attack check next frame.
		var mc = self.GetComponent<MovementComponent>();
		mc?.OnTriggerMove(self, _moveTargetGO.transform);
	}

	private void StopMovement(Transform self)
	{
		var pf = self.GetComponent<UnitPathFollower>();
		if (pf != null) pf.StopAllCoroutines();
	}

	private void Cleanup(Transform self)
	{
		if (self != null) StopMovement(self);
		if (_moveTargetGO != null)
		{
			Object.Destroy(_moveTargetGO);
			_moveTargetGO = null;
		}
		_enemy = null;
		// Clear shared blackboard keys so lower-priority sequences don't resume
		// toward stale destinations after combat ends.
		bb.Set<Transform>("target", null);
		bb.Set<Transform>("itemTarget", null);
		bb.Set<WorldItem>("targetWorldItem", null);
	}
}