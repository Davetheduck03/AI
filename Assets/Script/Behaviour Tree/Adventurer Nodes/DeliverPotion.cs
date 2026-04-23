using UnityEngine;

/// <summary>
/// ACTION: Emergency potion delivery — runs toward a critically hurt ally and
/// hands the potion over on arrival, even while enemies are present.
///
/// WHY THIS EXISTS
///   SharePotion is gated by NoRevealedEnemies so the donor isn't recklessly
///   walking into combat just to share.  But when an ally is FLEEING at near-
///   death HP that gate is exactly wrong: the ally is moving away from the donor,
///   the donor won't pursue, and the ally dies before the area ever clears.
///
///   DeliverPotion fires at a higher priority than SharePotion and has NO enemy
///   guard.  It only activates for genuinely critical situations (ally HP below
///   criticalHPFraction, default 0.40) so the donor isn't sprinting across the
///   map every time someone takes a scratch.
///
/// FUZZY SCORE
///   score = urgency × surplusWeight
///     urgency      = RampDown(allyHP, 0, criticalHPFraction)
///                    → 1 when nearly dead, 0 at criticalHPFraction
///     surplusWeight= Ramp(donorPotions, 1, 3)
///                    → scales up as the donor holds more spare potions
///                    → prevents a donor with exactly 2 potions from running
///                       across the entire map for a mildly injured ally
///   Passes when score ≥ deliveryThreshold (default 0.25)
///
/// FLOW PER TICK
///   1. Hard gate: donor holds 2+ HP potions.
///   2. Find best critical ally (highest fuzzy score) within scanRange.
///   3. Within deliveryRange → transfer potion → Success.
///      Outside deliveryRange → move toward ally → Running.
///   4. Ally healed above criticalHPFraction mid-chase → abort → Failure.
///
/// MOVEMENT
///   Uses MovementComponent.OnTriggerMove with a hidden marker Transform
///   (same pattern as FleeFromNearestEnemy) so A* is called normally.
///   Re-paths every RePathInterval seconds or when the ally moves significantly.
/// </summary>
public class DeliverPotion : Node
{
    // ── Parameters ────────────────────────────────────────────────────────────
    private readonly float _scanRange;           // how far to search for a recipient
    private readonly float _deliveryRange;       // hand-off distance
    private readonly float _criticalHPFraction;  // ally must be below this to trigger
    private readonly float _deliveryThreshold;   // minimum fuzzy score to commit

    // ── Movement state ────────────────────────────────────────────────────────
    private Transform _deliveryMarker;           // hidden child GO used as A* target
    private Transform _currentTarget;            // ally we're running toward
    private const float RePathInterval = 0.4f;
    private float _lastRePathTime = -999f;
    private Vector2 _lastTargetPos = Vector2.positiveInfinity;
    private const float TargetMoveThreshold = 1.0f;

    public DeliverPotion(Blackboard bb,
                         float scanRange          = 20f,
                         float deliveryRange      = 2.0f,
                         float criticalHPFraction = 0.40f,
                         float deliveryThreshold  = 0.25f) : base(bb)
    {
        _scanRange          = scanRange;
        _deliveryRange      = deliveryRange;
        _criticalHPFraction = criticalHPFraction;
        _deliveryThreshold  = deliveryThreshold;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var myEquip = self.GetComponent<EquipmentComponent>();
        if (myEquip == null || myEquip.TotalHealthPotions <= 1)
        {
            if (_currentTarget != null) Abort(self);
            return NodeState.Failure;   // keep at least one for self
        }

        // ── Find best critical ally ───────────────────────────────────────────
        Transform best      = null;
        float     bestScore = _deliveryThreshold;

        float donorPotions = myEquip.TotalHealthPotions;
        // Surplus weight: ramps from 0→1 as donor goes from 1→3 potions.
        // A donor with 2 potions scores 0.5; with 3+ scores 1.0.
        float surplusWeight = FuzzyLogic.Ramp(donorPotions, 1f, 3f);

        foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (p == null || p.transform == self) continue;

            var hc = p.GetComponent<HealthComponent>();
            if (hc == null || hc.currentHealth <= 0f) continue;

            // Recipient must be critically hurt
            float hpFrac = hc.currentHealth / hc.maxHealth;
            if (hpFrac >= _criticalHPFraction) continue;

            // Recipient must have room for a potion
            var equip = p.GetComponent<EquipmentComponent>();
            if (equip == null || !HasRoomForHealthPotion(equip)) continue;

            float dist = Vector2.Distance(self.position, p.transform.position);
            if (dist > _scanRange) continue;

            // urgency: 1 when nearly dead, 0 at criticalHPFraction
            float urgency = FuzzyLogic.RampDown(hpFrac, 0f, _criticalHPFraction);
            float score   = urgency * surplusWeight;

            if (score > bestScore)
            {
                bestScore = score;
                best      = p.transform;
            }
        }

        if (best == null)
        {
            // Only stop the path if we were mid-delivery — don't interrupt
            // an unrelated Explore or Follow path that's already running.
            if (_currentTarget != null) Abort(self);
            else _currentTarget = null;
            return NodeState.Failure;
        }

        _currentTarget = best;

        // ── Within delivery range — hand over the potion ──────────────────────
        float distToTarget = Vector2.Distance(self.position, best.position);
        if (distToTarget <= _deliveryRange)
        {
            HealthPotionSO given = myEquip.GiveHealthPotion();
            if (given == null) { Abort(self); return NodeState.Failure; }

            var recipientEquip = best.GetComponent<EquipmentComponent>();
            bool accepted = recipientEquip.TryAddHealthPotion(given);

            if (!accepted)
            {
                // Recipient unexpectedly full — return potion to self, then stop delivery
                myEquip.TryAddHealthPotion(given);
                Debug.LogWarning($"[DeliverPotion] {self.name} → {best.name} rejected potion; returned to donor.");
                _currentTarget = null;
                _lastTargetPos = Vector2.positiveInfinity;
                return NodeState.Failure;
            }

            var hc = best.GetComponent<HealthComponent>();
            float hpFrac = hc != null ? hc.currentHealth / hc.maxHealth : 0f;
            Debug.Log($"[DeliverPotion] {self.name} delivered HP potion to {best.name} " +
                      $"(HP {hpFrac:P0}) — donor has {myEquip.TotalHealthPotions} left");

            Abort(self);
            return NodeState.Success;
        }

        // ── Still moving — (re-)path toward the ally ──────────────────────────
        Vector2 targetPos   = best.position;
        float   targetMoved = Vector2.Distance(targetPos, _lastTargetPos);
        bool    timeToRePath = Time.time - _lastRePathTime >= RePathInterval;

        if (timeToRePath || targetMoved >= TargetMoveThreshold)
        {
            EnsureDeliveryMarker(self);
            _deliveryMarker.position = targetPos;
            _lastRePathTime          = Time.time;
            _lastTargetPos           = targetPos;

            var mc = self.GetComponent<MovementComponent>();
            if (mc != null)
                mc.OnTriggerMove(self, _deliveryMarker);

            Debug.Log($"[DeliverPotion] {self.name} running to {best.name} " +
                      $"({distToTarget:F1}u away, score {bestScore:F2})");
        }

        return NodeState.Running;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void Abort(Transform self)
    {
        _currentTarget = null;
        _lastTargetPos = Vector2.positiveInfinity;

        // Stop movement so the hero doesn't keep walking to an old position
        if (self != null)
            self.GetComponent<UnitPathFollower>()?.StopPath();
    }

    private void EnsureDeliveryMarker(Transform hero)
    {
        if (_deliveryMarker != null) return;
        var go = new GameObject("[DeliveryMarker]")
        {
            hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector
        };
        go.transform.SetParent(hero, worldPositionStays: false);
        _deliveryMarker = go.transform;
    }

    private static bool HasRoomForHealthPotion(EquipmentComponent equip)
    {
        bool slot1Room = equip.equippedHealthPotion == null
                      || equip.healthPotionCount < equip.equippedHealthPotion.maxStack;
        bool slot2Room = equip.equippedHealthPotion2 == null
                      || equip.healthPotionCount2 < equip.equippedHealthPotion2.maxStack;
        return slot1Room || slot2Room;
    }
}
