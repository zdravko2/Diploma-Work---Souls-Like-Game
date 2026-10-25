using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Character Effect/Timed/Damage Over Time Effect",
                fileName = "New DamageOverTimeEffect")]
public class DamageOverTimeEffect : TimedCharacterEffect
{
    public DamageData damageOverTime = new DamageData() { damageType = DamageType.Effect, physical = 1};
    public override void OnTickEffect(CharacterManager target, float deltaTime)
    {
        base.OnTickEffect(target, deltaTime);
        
        if (target.statusManager.isDead) return;
        target.statusManager.InflictDamage(damageOverTime * Time.deltaTime);
    }
}
