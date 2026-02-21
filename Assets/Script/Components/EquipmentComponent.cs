using UnityEngine;

public class EquipmentComponent : UnitComponent
{
    public WeaponSO equippedWeapon { get; private set; }
    public HeadArmorSO equippedHead { get; private set; }
    public BodyArmorSO equippedBody { get; private set; }

    private HealthComponent healthComp;
    private DamageComponent damageComp;

    private float appliedWeaponDamage = 0f;
    private float appliedHeadArmor = 0f;
    private float appliedBodyArmor = 0f;

    protected override void OnInitialize()
    {
        healthComp = GetComponent<HealthComponent>();
        damageComp = GetComponent<DamageComponent>();
    }

    public bool TryEquip(ItemSO newItem)
    {
        if (newItem is WeaponSO weapon) return TryEquipWeapon(weapon);
        if (newItem is HeadArmorSO headArmor) return TryEquipHead(headArmor);
        if (newItem is BodyArmorSO bodyArmor) return TryEquipBody(bodyArmor);

        Debug.LogWarning($"[EquipmentComponent] Unknown type: {newItem.GetType()}");
        return false;
    }

    // ─────────────────────────────────────────────
    // WEAPON
    // ─────────────────────────────────────────────

    private bool TryEquipWeapon(WeaponSO newWeapon)
    {
        if (equippedWeapon != null && newWeapon.GetScore() <= equippedWeapon.GetScore())
        {
            Debug.Log($"[Equipment] Kept {equippedWeapon.itemName} " +
                      $"({equippedWeapon.GetScore():F1}) over " +
                      $"{newWeapon.itemName} ({newWeapon.GetScore():F1})");
            return false;
        }

        if (equippedWeapon != null)
            damageComp?.AddDamageBonus(-appliedWeaponDamage);

        equippedWeapon = newWeapon;
        appliedWeaponDamage = newWeapon.statValue;
        damageComp?.AddDamageBonus(appliedWeaponDamage);

        Debug.Log($"[Equipment] {gameObject.name} equipped {newWeapon.itemName}");
        return true;
    }

    // ─────────────────────────────────────────────
    // HEAD ARMOR
    // ─────────────────────────────────────────────

    private bool TryEquipHead(HeadArmorSO newHead)
    {
        if (equippedHead != null && newHead.GetScore() <= equippedHead.GetScore())
        {
            Debug.Log($"[Equipment] Kept {equippedHead.itemName} over {newHead.itemName}");
            return false;
        }

        if (equippedHead != null)
            healthComp?.AddArmorBonus(-appliedHeadArmor);

        equippedHead = newHead;
        appliedHeadArmor = newHead.statValue;
        healthComp?.AddArmorBonus(appliedHeadArmor);

        Debug.Log($"[Equipment] {gameObject.name} equipped {newHead.itemName} " +
                  $"(+{appliedHeadArmor} armor → {healthComp?.DamageReduction:P0} total reduction)");
        return true;
    }

    // ─────────────────────────────────────────────
    // BODY ARMOR
    // ─────────────────────────────────────────────

    private bool TryEquipBody(BodyArmorSO newBody)
    {
        if (equippedBody != null && newBody.GetScore() <= equippedBody.GetScore())
        {
            Debug.Log($"[Equipment] Kept {equippedBody.itemName} over {newBody.itemName}");
            return false;
        }

        if (equippedBody != null)
            healthComp?.AddArmorBonus(-appliedBodyArmor);

        equippedBody = newBody;
        appliedBodyArmor = newBody.statValue;
        healthComp?.AddArmorBonus(appliedBodyArmor);

        Debug.Log($"[Equipment] {gameObject.name} equipped {newBody.itemName} " +
                  $"(+{appliedBodyArmor} armor → {healthComp?.DamageReduction:P0} total reduction)");
        return true;
    }

    public void LogLoadout()
    {
        Debug.Log($"[{gameObject.name} Loadout] " +
                  $"Weapon: {(equippedWeapon != null ? equippedWeapon.itemName : "none")} | " +
                  $"Head: {(equippedHead != null ? equippedHead.itemName : "none")} | " +
                  $"Body: {(equippedBody != null ? equippedBody.itemName : "none")} | " +
                  $"Total Armor: {healthComp?.totalArmor} " +
                  $"({healthComp?.DamageReduction:P0} reduction)");
    }
}