using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DummyAIController : CharacterControlsManager
{
    [Header("Offensive Options")]
    public bool alwaysAttack = false;
    float lastAttackTime = 0;
    public float attackCooldown = 3f;
    public bool fullCombo = false;

    [Header("Defensive Options")]
    public bool alwaysBlock = false;
    public bool alwaysParry = false;
    public bool alwaysDodge = false;


    protected override void Start()
    {
        base.Start();
        character.characterGroup = CharacterGroup.Neutral;

        if (character.inventoryManager.GetByName("Enemy Sword") is WeaponItem weaponItem)
        {
            character.equipmentManager.AssignWeaponToMainHand(weaponItem);
        }
        character.equipmentManager.EquipMainHandWeapon();
    }

    protected override void Update()
    {
        base.Update();
        character.animatorManager.applyRootMotion = false;
        character.locomotionManager.canMove = false;

        PerformAttack();
        PerformBlock();
        PerformParry();
        PerformDodge();
    }

    void PerformAttack()
    {
        primaryActionInput = fullCombo;

        if (!alwaysAttack) return;
        if (attackCooldown > 0 && Time.time - lastAttackTime < attackCooldown) return;

        if (character.combatManager.canAttack)
        {
            WeaponItem weapon = character.equipmentManager.mainHandWeaponManager.weaponItem;
            character.combatManager.PerformWeaponAction(weapon.primaryAction, character.equipmentManager.mainHandWeaponManager);
            lastAttackTime = Time.time;
        }
    }

    void PerformBlock()
    {
        if (!alwaysBlock) return;
        character.combatManager.isBlocking = alwaysBlock;
    }

    void PerformParry()
    {
        if (!alwaysParry) return;
        character.combatManager.isParrying = alwaysParry;
    }

    void PerformDodge()
    {
        if (!alwaysDodge) return;
        character.combatManager.hasInvincibility = alwaysDodge;
    }
}
