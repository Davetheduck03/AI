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

	public KiteAndAttack(Blackboard bb, LayerMask targetLayer, float kiteDistance = 3.5f)
		: base(bb)
	{
		this.kiteDistance = kiteDistance;
		_targetLayer = targetLayer;
	}

	// ── Evaluate ──────────────────────────────────────────────────────────────

	public override NodeState Evaluate()
	{
		Transform self = bb.Get<Transform>("self");

		// Read the enemy from the blackboard ONLY on the first call or when
		// the blackboard has a fresh enemy (set by FindNearestRevealedEnemy).
		Transform bbEnemy = bb.Get<Transform>("target");
		if (bbEnemy != null && bbEnemy != _moveTargetGO?.transform)
			_enemy = bbEnemy;   // real enemy — accept it

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
		if (dist <= effectiveAttackRange &&
			Time.time - _lastAttackTime >= damageComp.AttackCooldown)
		{
			damageComp.TryDealDamage(_enemy.gameObject);
			_lastAttackTime = Time.time;
			Debug.Log($"[KiteAndAttack] {self.name} hit {_enemy.name} " +
					  $"dist={dist:F2} state={_state}");
			return NodeState.Success;
		}

		return NodeState.Running;
	}

	// ── Movement helpers ──────────────────────────────────────────────────────

	private bool TryRetreat(Transform self)
	{
		// Stuck check
		if (Time.time >= _nextStuckCheckAt)
		{
			float moved = Vector3.Distance(self.position, _lastCheckedPos);
			_lastCheckedPos = self.position;
			_nextStuckCheckAt = Time.time + MovementCheckInterval;

			if (moved < StuckDistanceThreshold)
			{
				Debug.Log($"[KiteAndAttack] {self.name} stuck retreating — holding.");
				return false;
			}
		}

		if (Time.time < _nextMoveRetrigger) return true;   // already moving

		Vector3 awayDir = (self.position - _enemy.position).normalized;
		float stepBack = Mathf.Max(kiteDistance - Vector2.Distance(self.position, _enemy.position)
									 + RetreatTriggerMargin + 1.0f, 1.5f);
		TriggerMove(self, self.position + awayDir * stepBack);
		_nextMoveRetrigger = Time.time + MoveRetriggerInterval;
		return true;
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
	}
}