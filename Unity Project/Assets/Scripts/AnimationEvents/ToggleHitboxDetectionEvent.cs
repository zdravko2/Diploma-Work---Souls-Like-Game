using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#00fff2")]
[AnimatorStateEventMenu("Combat/Toggle Hitbox Detection")]
public sealed class ToggleHitboxDetectionEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField] KeyValueHitboxPair hitBoxSettings = new KeyValueHitboxPair();
    [SerializeField] DamageData damageMultiplier = new DamageData(physical: 1f, magical: 1f, poise: 1f, breaksHyperArmor: false, stamina: 1f, knockbackStrength: 1f);

    [Serializable]
    private class KeyValueHitboxPair
    {
        public string name;
        public bool value;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (character == null) return;
        
        character.combatManager.ToggleHitBoxDetection(hitBoxSettings.name, hitBoxSettings.value);
        character.combatManager.GetHitBox(hitBoxSettings.name).damageMultiplier = damageMultiplier;

        if (character.soundFXManager != null && hitBoxSettings.value == true)
        {
            character.soundFXManager.PlaySoundEffect(character.soundFXManager.swingAttackSound);
        }
    }
}
