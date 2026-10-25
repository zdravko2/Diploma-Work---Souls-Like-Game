using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventMenu("Animation/Toggle Special Flag")]
[AnimatorStateEventColor("#ff9100")]
public class ToggleSpecialFlagEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField] SpecialFlag specialFlag;
    [SerializeField] bool value;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        switch (specialFlag)
        {
            case SpecialFlag.UseRootMotion:
                character.animatorManager.applyRootMotion = value;
                break;
            case SpecialFlag.CanBeInterrupted:
                character.animatorManager.canBeInterrupted = value;
                break;
            case SpecialFlag.HasInvincibility:
                character.combatManager.hasInvincibility = value;
                break;
            case SpecialFlag.HasHyperArmor:
                character.combatManager.hasHyperArmor = value;
                break;
            case SpecialFlag.CanPerformNextComboAttack:
                character.combatManager.canPerformNextComboAttack = value;
                break;
            case SpecialFlag.CanBeLockedOn:
                character.combatManager.canBeLockedOn = value;
                break;
            case SpecialFlag.IsExposed:
                character.combatManager.isExposed = value;
                break;
            default:
                Debug.LogWarning($"[{animator.name}] Property not selected in state [{manager.statePath}] at frame [{frame}]");
                break;
        }
    }

    private enum SpecialFlag
    {
        None = 0,
        UseRootMotion = 10,
        CanBeInterrupted = 20,
        HasInvincibility = 30,
        HasHyperArmor = 40,
        CanPerformNextComboAttack = 50,
        CanBeLockedOn = 60,
        IsExposed = 70
    }
}
