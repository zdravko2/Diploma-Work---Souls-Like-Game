using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#00ffea")]
[AnimatorStateEventMenu("Status/Apply Effect")]
public class ApplyEffectEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField] CharacterEffect effect;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (character == null) return;

        character.effectsManager.ApplyEffect(effect);
    }
}
