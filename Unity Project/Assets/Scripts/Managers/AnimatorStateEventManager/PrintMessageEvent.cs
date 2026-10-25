using System;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#ff0000")]
[AnimatorStateEventMenu("Debug/Print Message")]
public sealed class PrintMessageEvent : AnimatorStateEvent
{
    public string message = "";
    
    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (string.IsNullOrEmpty(message)) return;
        Debug.Log(animator.gameObject.name + ": " + message);
    }
}