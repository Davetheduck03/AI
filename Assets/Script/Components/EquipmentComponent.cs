using UnityEngine;

public class EquipmentComponent : UnitComponent
{
    public WeaponSO equippedWeapon { get; private set; }
    public HeadArmorSO equippedHead { get; private set; }
    public BodyArmorSO equippedBody { get; private set; }

    private HealthComponent healthComp;
    private DamageComponent damageComp;

    // Cached class reference for weapon restriction checks
    private AdventurerClassSO adventurerClass;

    private float appliedWeaponDamage = 0f;
    private float appliedWeaponAttackSpeed = 0f;
    private float appliedHeadArmor = 0f;
    private float appliedBodyArmor = 0f;

    protected override void OnInitialize()
    {
        healthComp = GetComponent<HealthComponent>();
        damageComp = GetComponent<DamageComponent>();

        // Pull class reference from HeroSO if available
        if (data is HeroSO heroData)
        {
            adventurerClass = heroData.adventurerClass;

            if (adventurerClass == null)
                Debug.LogWarning($"[EquipmentComponent] {gameObject.name} has no AdventurerClass assigned in HeroSO!");
            else
                Debug.Log($"[EquipmentComponent] {gameObject.name} initialized as {adventurerClass.className}");
        }
    }

    public bool TryEquip(ItemSO newItem)
    {
        if (newItem is WeaponSO weapon) return TryEquipWeapon(weapon);
        if (newItem is HeadArmorSO head) return TryEquipHead(head);
        if (newItem is BodyArmorSO body) return TryEquipBody(body);

        Debug.LogWarning($"[EquipmentComponent] Unknown item type: {newItem.GetType()}");
        return false;
    }

    // ─────────────────────────────────────────────
    // WEAPON
    // ─────────────────────────────────────────────

    private bool TryEquipWeapon(WeaponSO newWeapon)
    {
        // Class restriction check
        if (adventurerClass != null && !adventurerClass.CanEquipWeapon(newWeapon))
        {
            Debug.Log($"[Equipment] {gameObject.name} ({adventurerClass.className}) " +
                      $"cannot equip {newWeapon.itemName} " +
                      $"(WeaponType: {newWeapon.weaponType} not allowed for this class)");
            return false;
        }

        // Score check — keep better weapon
        if (equippedWeapon != null && newWeapon.GetScore() <= equippedWeapon.GetScore())
        {
            Debug.Log($"[Equipment] Kept {equippedWeapon.itemName} " +
                      $"({equippedWeapon.GetScore():F1}) over " +
                      $"{newWeapon.itemName} ({newWeapon.GetScore():F1})");
            return false;
        }

        // Strip old weapon bonuses before applying new ones
        if (equippedWeapon != null)
        {
            damageComp?.AddDamageBonus(-appliedWeaponDamage);
            damageComp?.AddAttackSpeedBonus(-appliedWeaponAttackSpeed);
        }

        equippedWeapon = newWeapon;
        appliedWeaponDamage = newWeapon.damageBonus;
        appliedWeaponAttackSpeed = newWeapon.attackSpeedBonus;

        damageComp?.AddDamageBonus(appliedWeaponDamage);
        damageComp?.AddAttackSpeedBonus(appliedWeaponAttackSpeed);

        Debug.Log($"[Equipment] {gameObject.name} equipped {newWeapon.itemName} " +
                  $"(+{appliedWeaponDamage} dmg, +{appliedWeaponAttackSpeed} atk spd) → " +
                  $"Total: {damageComp?.TotalDamage} dmg, {damageComp?.TotalAttackSpeed}/s");
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
                  $"Class: {(adventurerClass != null ? adventurerClass.className : "none")} | " +
                  $"Weapon: {(equippedWeapon != null ? equippedWeapon.itemName : "none")} | " +
                  $"Head: {(equippedHead != null ? equippedHead.itemName : "none")} | " +
                  $"Body: {(equippedBody != null ? equippedBody.itemName : "none")} | " +
                  $"Total Armor: {healthComp?.totalArmor} " +
                  $"({healthComp?.DamageReduction:P0} reduction)");
    }
}