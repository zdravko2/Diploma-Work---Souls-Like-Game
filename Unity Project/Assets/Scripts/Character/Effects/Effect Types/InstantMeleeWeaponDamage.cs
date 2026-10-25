using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstantMeleeWeaponDamageEffect : InstantCharacterEffect
{
    [Header("Damage")]
    public DamageData damage;
    public override void OnStartEffect(CharacterManager target)
    {
        base.OnStartEffect(target);

        target.statusManager.InflictDamage(damage);
    }
}
