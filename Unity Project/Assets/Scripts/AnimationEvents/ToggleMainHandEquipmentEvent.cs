using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[AnimatorStateEventMenu("Animation/Toggle Main Hand Equipment")]
[AnimatorStateEventColor("#ff5100")]
public class ToggleMainHandEquipmentEvent : AnimatorStateEvent
{
    [SerializeField] private bool showMainHandEquipment = true;

    PlayerManager playerManager;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (playerManager == null) playerManager = animator.GetComponent<PlayerManager>();
    }
    
    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (playerManager == null) return;
        if (playerManager.equipmentManager is not PlayerEquipmentManager equipmentManager) return;

        if (showMainHandEquipment)
        {
            equipmentManager.EquipMainHandWeapon();
        }
        else
        {
            equipmentManager.UnequipMainHandWeapon();
        }
    }
}
