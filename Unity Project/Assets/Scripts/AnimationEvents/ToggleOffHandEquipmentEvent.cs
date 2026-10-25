using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventMenu("Animation/Toggle Off Hand Equipment")]
[AnimatorStateEventColor("#ff5100")]
public class ToggleOffHandEquipmentEvent : AnimatorStateEvent
{
    [SerializeField] private bool showOffHandEquipment = true;

    PlayerManager playerManager;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (playerManager == null) playerManager = animator.GetComponent<PlayerManager>();
    }
    
    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (playerManager == null) return;
        if (playerManager.equipmentManager is not PlayerEquipmentManager equipmentManager) return;

        if (showOffHandEquipment)
        {
            equipmentManager.EquipOffHandWeapon();
        }
        else
        {
            equipmentManager.UnequipOffHandWeapon();
        }
    }
}
