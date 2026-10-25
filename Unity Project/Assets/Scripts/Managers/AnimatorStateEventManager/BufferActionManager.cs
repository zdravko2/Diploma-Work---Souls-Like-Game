using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AnimatorStateEventManager))]
public class BufferActionManager : StateMachineBehaviour
{
    CharacterManager character;
    bool queueNextAction = false;
    [SerializeField] string nextActionAnimation = "Next Animation";
    [SerializeField] BufferType bufferType;

    private enum BufferType
    {
        None = 0,
        BufferNextPrimaryAttack = 10,
        BufferNextSecondaryAttack = 20,
    }

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (character == null)
        {
            character = animator.GetComponent<CharacterManager>();
        }
        queueNextAction = false;
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        switch (bufferType)
        {
            case BufferType.BufferNextPrimaryAttack:
                HandleBufferingNextPrimaryAttack(animator, stateInfo, layerIndex);
                break;
            case BufferType.BufferNextSecondaryAttack:
                HandleBufferingNextSecondaryAttack(animator, stateInfo, layerIndex);
                break;
            default:
                Debug.LogWarning($"[{animator.name}] Buffer Type not selected for action {nextActionAnimation}");
                break;
        }
    }

    private void HandleBufferingNextPrimaryAttack(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (character.controller.primaryActionInput)
        {
            queueNextAction = true;
        }

        if (queueNextAction && character.combatManager.canPerformNextComboAttack && !animator.IsInTransition(layerIndex) && character.equipmentManager.hasMainHandWeapon)
        {
            if (character.statusManager.currentStamina < 1) return;
            if (!character.equipmentManager.isMainHandWeaponHeld)
            {
                character.equipmentManager.EquipMainHandWeapon();
            }
            
            if (character.equipmentManager.isMainHandWeaponHeld)
            {
                character.animatorManager.PlayTargetAttackAnimation(nextActionAnimation);
                queueNextAction = false;
            }
        }
    }

    private void HandleBufferingNextSecondaryAttack(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (character.controller.secondaryActionInput)
        {
            queueNextAction = true;
        }

        if (queueNextAction && character.combatManager.canPerformNextComboAttack && !animator.IsInTransition(layerIndex) && character.equipmentManager.hasMainHandWeapon)
        {
            if (character.statusManager.currentStamina < 1) return;
            if (!character.equipmentManager.isMainHandWeaponHeld)
            {
                character.equipmentManager.EquipMainHandWeapon();
            }
            
            if (character.equipmentManager.isMainHandWeaponHeld)
            {
                character.animatorManager.PlayTargetAttackAnimation(nextActionAnimation);
                queueNextAction = false;
            }
        }
    }
}
