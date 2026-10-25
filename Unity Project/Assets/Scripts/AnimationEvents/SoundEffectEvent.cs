using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#ae00ff")]
[AnimatorStateEventMenu("Effects/Sound Effect")]
public class SoundEffectEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField] AudioClip soundEffect;
    [SerializeField] float volume = 1f;
    [SerializeField] float pitch = 1f;
    [SerializeField] float randomPitchRange = 0.1f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }
    
    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (soundEffect == null || character == null || character.soundFXManager == null) return;
        
        if (character.characterController.isGrounded)
        character.soundFXManager.PlaySoundEffect(soundEffect, volume, pitch, randomPitchRange);
    }
}