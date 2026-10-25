using System.Collections;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAICombatManager : CharacterCombatManager
{
    public override void HandleReceiveDamage(DamageData damageData)
    {
        base.HandleReceiveDamage(damageData);

        lockedOnTarget = damageData.attacker;

        if (lockedOnTarget.statusManager is not PlayerStatusManager playerStatusManager) return;
        playerStatusManager.RegainMana(damageData.physical / 2);
    }
}
