using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventMenu("Animation/Toggle Action Flag")]
[AnimatorStateEventColor("#ffff00")]
public class ToggleActionFlagEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField] ActionFlag actionFlag;
    [SerializeField] bool value;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        switch (actionFlag)
        {
            case ActionFlag.IsAttacking:
                character.combatManager.isAttacking = value;
                break;
            case ActionFlag.IsBlocking:
                character.combatManager.isBlocking = value;
                break;
            case ActionFlag.IsParrying:
                character.combatManager.isParrying = value;
                break;
            case ActionFlag.IsDodging:
                character.combatManager.isDodging = value;
                break;
            case ActionFlag.IsHealing:
                character.combatManager.isHealing = value;
                break;
            case ActionFlag.IsHitReacting:
                character.combatManager.isHitReacting = value;
                break;
            case ActionFlag.IsStunned:
                character.combatManager.isStunned = value;
                break;
            case ActionFlag.IsExposed:
                character.combatManager.isExposed = value;
                break;
            default:
                Debug.LogWarning($"[{animator.name}] Property not selected in state [{manager.statePath}] at frame [{frame}]");
                break;
        }
    }

    private enum ActionFlag
    {
        None = 0,
        IsAttacking = 10,
        IsBlocking = 20,
        IsParrying = 30,
        IsDodging = 40,
        IsHealing = 50,
        IsHitReacting = 60,
        IsStunned = 70,
        IsExposed = 80,
    }
}
