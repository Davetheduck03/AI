// AdaptiveAttack.cs
using UnityEngine;

/// <summary>
/// Delegates to KiteAndAttack or MoveAndAttack depending on the weapon
/// currently equipped by the unit.
///
/// Ranged weapon types (Bow) → KiteAndAttack
/// Everything else           → MoveAndAttack
///
/// The check happens every tick so swapping a weapon mid-run immediately
/// changes the combat behaviour without requiring a tree rebuild.
/// </summary>
public class AdaptiveAttack : Node
{
    private readonly LayerMask _targetLayer;
    private readonly float     _kiteDistance;

    // Lazily constructed — only created when the weapon type that needs them
    // is first encountered, so there is no upfront cost for units that never
    // change weapon class.
    private KiteAndAttack  _kiteNode;
    private MoveAndAttack  _meleeNode;

    private WeaponType _lastWeaponType = (WeaponType)(-1);  // sentinel — "not yet checked"

    /// <param name="kiteDistance">
    /// Preferred standoff range passed to KiteAndAttack when a bow is equipped.
    /// Ignored for melee weapons.
    /// </param>
    public AdaptiveAttack(Blackboard bb, LayerMask targetLayer, float kiteDistance = 3.5f)
        : base(bb)
    {
        _targetLayer  = targetLayer;
        _kiteDistance = kiteDistance;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        WeaponType currentType = GetEquippedWeaponType(self);

        // Log only when the weapon type actually changes
        if (currentType != _lastWeaponType)
        {
            Debug.Log($"[AdaptiveAttack] {self.name} switched to {currentType} " +
                      $"→ {(IsRanged(currentType) ? "kite" : "melee")} mode");
            _lastWeaponType = currentType;
        }

        if (IsRanged(currentType))
        {
            _kiteNode ??= new KiteAndAttack(bb, _targetLayer, _kiteDistance);
            return _kiteNode.Evaluate();
        }
        else
        {
            _meleeNode ??= new MoveAndAttack(bb, _targetLayer);
            return _meleeNode.Evaluate();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsRanged(WeaponType type) => type == WeaponType.Bow;

    private static WeaponType GetEquippedWeaponType(Transform self)
    {
        var equipment = self.GetComponent<EquipmentComponent>();
        if (equipment?.equippedWeapon != null)
            return equipment.equippedWeapon.weaponType;

        // No weapon equipped — treat as melee so the unit still closes in
        return WeaponType.Sword;
    }
}
