using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character Actions/Weapon/Combo Attack")]
public class ComboAttackWeaponAction : WeaponItemAction
{
    public string comboAnimation1 = "";
    public string comboAnimation2 = "";
    public string comboAnimation3 = "";
    public override void PerformAction(CharacterManager character, WeaponManager weapon)
    {
        base.PerformAction(character, weapon);

        if (!character.combatManager.canAttack) return;
        if (character.statusManager.currentStamina < staminaCost) return;

        //character.statusManager.LoseStamina(staminaCost);

        switch (character.combatManager.comboIndex)
        {
            case 0:
                character.animatorManager.PlayTargetAttackAnimation(comboAnimation1);
                break;
            case 1:
                //character.animatorManager.PlayTargetAttackAnimation(comboAnimation2);
                break;
            case 2:
                //character.animatorManager.PlayTargetAttackAnimation(comboAnimation3);
                break;
            default:
                break;
        }
    }
}
