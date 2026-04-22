using UnityEngine;

/// <summary>
/// ACTION: Fuzzy need-based potion sharing.
///
/// HOW IT WORKS
///   The hero gives a potion to the most urgently injured nearby ally,
///   provided the donor holds more than 1 of that potion type (never gives
///   away their last one) and the combined need score passes the threshold.
///
///   Hard gate: totalPotions > 1 (donor keeps at least 1 for themselves).
///
///   Fuzzy score  = urgency × proximity
///     urgency   = RampDown(allyHP,  urgLo=0.25, urgHi=0.65)
///                 → 1 when the ally is critically low, 0 at 65 % HP+
///     proximity = RampDown(distance, 0, searchRange)
///                 → prefers close allies, 0 at edge of searchRange
///
///   If score ≥ shareThreshold (default 0.30) the hero gives one potion.
///   The surplus factor was removed from the composite — it made scores too
///   small even in obvious cases (2 potions × 0.79 urgency × 0.70 proximity
///   = 0.18 < 0.40 even when the healer is nearly dead).  The hard gate
///   handles the "don't give away your last one" constraint more cleanly.
///
/// MANA POTION SHARING
///   Only to allies with attackManaCost > 0 OR healManaCost > 0 (Mage, Healer).
///
/// ROLLBACK SAFETY
///   GivePotion() removes from the donor first.  If TryAdd fails on the
///   recipient the potion is returned to the donor rather than vanishing.
/// </summary>
public class SharePotion : Node
{
    private readonly float _searchRange;
    private readonly float _shareThreshold;

    /// <param name="searchRange">   World-unit radius to scan for allies to share with.</param>
    /// <param name="shareThreshold">Minimum urgency × proximity score (default 0.30).</param>
    public SharePotion(Blackboard bb,
                       float searchRange    = 10f,
                       float shareThreshold = 0.30f) : base(bb)
    {
        _searchRange    = searchRange;
        _shareThreshold = shareThreshold;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var myEquip = self.GetComponent<EquipmentComponent>();
        if (myEquip == null) return NodeState.Failure;

        // Try HP potion sharing first, then mana — first successful share returns Success.
        if (TryShareHealthPotion(self, myEquip)) return NodeState.Success;
        if (TryShareManaPotion(self, myEquip))   return NodeState.Success;

        return NodeState.Failure;
    }

    // ── HP Potion ─────────────────────────────────────────────────────────────

    private bool TryShareHealthPotion(Transform self, EquipmentComponent myEquip)
    {
        int total = myEquip.TotalHealthPotions;
        if (total <= 1) return false;   // keep at least one for self

        // Find the best candidate — maximize urgency × proximity
        Transform bestAlly    = null;
        float     bestScore   = _shareThreshold;
        float     bestHPFrac  = 1f;

        foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (p == null || p.transform == self) continue;

            var hc = p.GetComponent<HealthComponent>();
            if (hc == null || hc.currentHealth <= 0f) continue;

            // Recipient must have room for at least one more HP potion
            var equip = p.GetComponent<EquipmentComponent>();
            if (equip == null) continue;
            if (!HasRoomForHealthPotion(equip)) continue;

            float dist     = Vector2.Distance(self.position, p.transform.position);
            if (dist > _searchRange) continue;

            float hpFrac   = hc.currentHealth / hc.maxHealth;
            float urgency  = FuzzyLogic.RampDown(hpFrac, 0.25f, 0.65f);
            float prox     = FuzzyLogic.RampDown(dist,   0f,    _searchRange);
            float score    = urgency * prox;   // surplus factor removed — hard gate above handles it

            if (score > bestScore)
            {
                bestScore  = score;
                bestAlly   = p.transform;
                bestHPFrac = hpFrac;
            }
        }

        if (bestAlly == null) return false;

        // Transfer the potion
        HealthPotionSO given = myEquip.GiveHealthPotion();
        if (given == null) return false;

        var recipientEquip = bestAlly.GetComponent<EquipmentComponent>();
        bool accepted = recipientEquip.TryAddHealthPotion(given);

        if (!accepted)
        {
            // Recipient somehow full now — use it ourselves rather than lose it
            myEquip.TryAddHealthPotion(given);
            Debug.LogWarning($"[SharePotion] {self.name} → {bestAlly.name} rejected HP potion; " +
                             "returned to donor.");
            return false;
        }

        Debug.Log($"[SharePotion] {self.name} gave HP potion to {bestAlly.name} " +
                  $"(HP {bestHPFrac:P0}, score {bestScore:F2}) — donor has {myEquip.TotalHealthPotions} left");
        return true;
    }

    // ── Mana Potion ───────────────────────────────────────────────────────────

    private bool TryShareManaPotion(Transform self, EquipmentComponent myEquip)
    {
        int total = myEquip.TotalManaPotions;
        if (total <= 1) return false;

        Transform bestAlly   = null;
        float     bestScore  = _shareThreshold;
        float     bestMFrac  = 1f;

        foreach (var p in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (p == null || p.transform == self) continue;

            var hc = p.GetComponent<HealthComponent>();
            if (hc == null || hc.currentHealth <= 0f) continue;

            // Only share mana with heroes who actually spend it
            var mc = p.GetComponent<ManaComponent>();
            if (mc == null) continue;
            if (mc.attackManaCost <= 0f && mc.healManaCost <= 0f) continue;

            // Recipient must have room for a mana potion
            var equip = p.GetComponent<EquipmentComponent>();
            if (equip == null) continue;
            if (!HasRoomForManaPotion(equip)) continue;

            float dist    = Vector2.Distance(self.position, p.transform.position);
            if (dist > _searchRange) continue;

            float mFrac   = mc.ManaFraction;
            float urgency = FuzzyLogic.RampDown(mFrac, 0.20f, 0.60f);
            float prox    = FuzzyLogic.RampDown(dist,  0f,    _searchRange);
            float score   = urgency * prox;   // surplus factor removed

            if (score > bestScore)
            {
                bestScore = score;
                bestAlly  = p.transform;
                bestMFrac = mFrac;
            }
        }

        if (bestAlly == null) return false;

        ManaPotionSO given = myEquip.GiveManaPotion();
        if (given == null) return false;

        var recipientEquip = bestAlly.GetComponent<EquipmentComponent>();
        bool accepted = recipientEquip.TryAddManaPotion(given);

        if (!accepted)
        {
            myEquip.TryAddManaPotion(given);
            Debug.LogWarning($"[SharePotion] {self.name} → {bestAlly.name} rejected mana potion; " +
                             "returned to donor.");
            return false;
        }

        Debug.Log($"[SharePotion] {self.name} gave mana potion to {bestAlly.name} " +
                  $"(mana {bestMFrac:P0}, score {bestScore:F2}) — donor has {myEquip.TotalManaPotions} left");
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool HasRoomForHealthPotion(EquipmentComponent equip)
    {
        bool slot1Room = equip.equippedHealthPotion == null
                      || equip.healthPotionCount < equip.equippedHealthPotion.maxStack;
        bool slot2Room = equip.equippedHealthPotion2 == null
                      || equip.healthPotionCount2 < equip.equippedHealthPotion2.maxStack;
        return slot1Room || slot2Room;
    }

    private static bool HasRoomForManaPotion(EquipmentComponent equip)
    {
        bool slot1Room = equip.equippedManaPotion == null
                      || equip.manaPotionCount < equip.equippedManaPotion.maxStack;
        bool slot2Room = equip.equippedManaPotion2 == null
                      || equip.manaPotionCount2 < equip.equippedManaPotion2.maxStack;
        return slot1Room || slot2Room;
    }
}
