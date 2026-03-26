using UnityEngine;

/// <summary>
/// CONDITION: Returns Success if the hero currently has a weapon of the specified
/// <see cref="WeaponType"/> equipped, Failure otherwise.
///
/// Use as a guard in sequences that should only run with a particular weapon:
///   • Mage attack sequence   — requires WeaponType.Staff
///   • Paladin heal sequences — requires WeaponType.Staff
/// </summary>
public class HasWeaponType : Node
{
    private readonly WeaponType _required;

    public HasWeaponType(Blackboard bb, WeaponType required) : base(bb)
    {
        _required = required;
    }

    public override NodeState Evaluate()
    {
        Transform self = bb.Get<Transform>("self");
        if (self == null) return NodeState.Failure;

        var eq = self.GetComponent<EquipmentComponent>();
        bool hasIt = eq?.equippedWeapon != null && eq.equippedWeapon.weaponType == _required;

        return hasIt ? NodeState.Success : NodeState.Failure;
    }
}
