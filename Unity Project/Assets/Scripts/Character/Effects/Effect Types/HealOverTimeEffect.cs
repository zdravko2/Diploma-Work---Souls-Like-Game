using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character Effect/Timed/Healing Over Time Effect",
                fileName = "New HealingOverTimeEffect")]
public class HealOverTimeEffect : TimedCharacterEffect
{
    public float healOverTime = 1;
    public override void OnTickEffect(CharacterManager target, float duration)
    {
        base.OnTickEffect(target, duration);

        target.statusManager.RegainHealth(healOverTime * Time.deltaTime);
    }
}
