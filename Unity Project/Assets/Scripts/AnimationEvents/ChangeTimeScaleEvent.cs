using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#5900ff")]
[AnimatorStateEventMenu("Effects/Change Time Scale")]
public class ChangeTimeScaleEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField] float timeScale = 1f;
    [SerializeField] float duration = 0.2f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }
    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (character == null) return;
        
        character.StartCoroutine(ChangeTimeScale());
    }

    IEnumerator ChangeTimeScale()
    {
        float original = 1; //Time.timeScale;

        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = original;
    }
}
