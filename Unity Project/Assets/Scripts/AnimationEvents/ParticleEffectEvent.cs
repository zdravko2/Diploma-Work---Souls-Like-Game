using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#ff008c")]
[AnimatorStateEventMenu("Effects/Particle Effect")]
public class ParticleEffectEvent : AnimatorStateEvent
{
    CharacterManager character;
    GameObject currentEffectInstance;
    [SerializeField] GameObject particleEffect;
    [SerializeField] float duration = 1f;
    [SerializeField] bool follow = false;
    [SerializeField] Vector3 localPositionOffset = Vector3.zero;
    [SerializeField] Vector3 localRotationOffset = Vector3.zero;
    
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (particleEffect != null)
        {
            CharacterVisualFXManager visualFXManager = animator.GetComponent<CharacterVisualFXManager>();
            if (visualFXManager != null)
            {
                currentEffectInstance = visualFXManager.PlayVisualEffectLocalSpace(particleEffect, localPositionOffset, localRotationOffset, follow, duration);
            }
        }
    }
    
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (currentEffectInstance != null)
        {
            GameObject.Destroy(currentEffectInstance);
            currentEffectInstance = null;
        }
    }
}