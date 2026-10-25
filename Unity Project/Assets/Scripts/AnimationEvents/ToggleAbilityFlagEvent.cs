using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventMenu("Animation/Toggle Ability Flag")]
[AnimatorStateEventColor("#00ff00FF")]
public class ToggleAbilityFlagEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField] AbilityFlag abilityFlag;
    [SerializeField] bool value;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        switch (abilityFlag)
        {
            case AbilityFlag.CanMove:
                character.locomotionManager.canMove = value;
                break;
            case AbilityFlag.CanRotate:
                character.locomotionManager.canRotate = value;
                break;
            case AbilityFlag.CanAttack:
                character.combatManager.canAttack = value;
                break;
            case AbilityFlag.CanBlock:
                character.combatManager.canBlock = value;
                break;
            case AbilityFlag.CanDodge:
                character.combatManager.canDodge = value;
                break;
            case AbilityFlag.CanHeal:
                character.combatManager.canHeal = value;
                break;
            case AbilityFlag.CanRun:
                character.locomotionManager.canRun = value;
                break;
            case AbilityFlag.CanJump:
                character.locomotionManager.canJump = value;
                break;
            default:
                Debug.LogWarning($"[{animator.name}] Property not selected in state [{manager.statePath}] at frame [{frame}]");
                break;
        }
    }

    private enum AbilityFlag
    {
        None = 0,
        CanMove = 10,
        CanRotate = 20,
        CanAttack = 30,
        CanBlock = 40,
        CanDodge = 50,
        CanHeal = 60,
        CanRun = 70,
        CanJump = 80,
    }
}
