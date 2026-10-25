using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character Effect/Static/Static Health Regen Effect",
                fileName = "New StaticHealthRegenEffect")]
public class ConstantHealthRegenEffect : StaticCharacterEffect
{
    public float regenPerSecond = 1f;
    public override void OnTickEffect(CharacterManager target, float deltaTime)
    {
        if (target == null) return;
        target.statusManager.RegainHealth(regenPerSecond * deltaTime);
    }
}
