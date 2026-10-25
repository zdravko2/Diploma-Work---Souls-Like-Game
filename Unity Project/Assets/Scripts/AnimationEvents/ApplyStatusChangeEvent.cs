using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#00b7ff")]
[AnimatorStateEventMenu("Status/Status Change")]
public class ApplyStatusChangeEvent : AnimatorStateEvent
{
    CharacterManager character;
    [SerializeField][Min(0)] float duration = 0f;
    [SerializeField] float healthChange = 0f;
    [SerializeField] float staminaChange = 0f;
    [SerializeField] float poiseChange = 0f;
    [SerializeField] float manaChange = 0f;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (character == null) return;

        if (healthChange != 0f || staminaChange != 0f || poiseChange != 0f || manaChange != 0f)
        {
            if (duration <= 0f)
                ApplyStatusImmediate();
            else
                character.StartCoroutine(ApplyStatusChangeOverTime(duration));
        }
    }
    
    void ApplyStatusImmediate()
    {
        if (healthChange > 0) character.statusManager.RegainHealth(healthChange);
        else if (healthChange < 0) character.statusManager.LoseHealth(-healthChange);

        if (staminaChange > 0) character.statusManager.RegainStamina(staminaChange);
        else if (staminaChange < 0) character.statusManager.LoseStamina(-staminaChange);

        if (poiseChange > 0) character.statusManager.RegainPoise(poiseChange);
        else if (poiseChange < 0) character.statusManager.LosePoise(-poiseChange);

        if (character.statusManager is PlayerStatusManager playerStatusManager)
        {
            if (manaChange > 0) playerStatusManager.RegainMana(manaChange);
            else if (manaChange < 0) playerStatusManager.LoseMana(-manaChange);
        }
    }

    IEnumerator ApplyStatusChangeOverTime(float duration)
    {
        float elapsed = 0f;

        do
        {
            float remaining = duration - elapsed;
            float deltaTime = Mathf.Min(Time.deltaTime, remaining);
            elapsed += deltaTime;

            float healthDelta = (healthChange / duration) * deltaTime;
            float staminaDelta = (staminaChange / duration) * deltaTime;
            float poiseDelta = (poiseChange / duration) * deltaTime;
            float manaDelta = (manaChange / duration) * deltaTime;

            if (healthDelta > 0) character.statusManager.RegainHealth(healthDelta);
            else if (healthDelta < 0) character.statusManager.LoseHealth(-healthDelta);

            if (staminaDelta > 0) character.statusManager.RegainStamina(staminaDelta);
            else if (staminaDelta < 0) character.statusManager.LoseStamina(-staminaDelta);

            if (poiseDelta > 0) character.statusManager.RegainPoise(poiseDelta);
            else if (poiseDelta < 0) character.statusManager.LosePoise(-poiseDelta);

            if (character.statusManager is PlayerStatusManager playerStatusManager)
            {
                if (manaDelta > 0) playerStatusManager.RegainMana(manaDelta);
                else if (manaDelta < 0) playerStatusManager.LoseMana(-manaDelta);
            }

            yield return null;
        }
        while (elapsed < duration);
    }
}
