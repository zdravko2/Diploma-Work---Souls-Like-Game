using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class PlayerController : CharacterControlsManager
{
    private PlayerInputManager playerInput;
    private Vector3 cameraLookDirection;
    private Vector3 cameraFront;
    private Vector3 cameraRight;
    public GameObject dialogueObject = null;
    float timeSinceMainHandWeaponActivity = 0f;
    bool lockOnSwitchReleased = true;
    float lockOnSwitchAccum = 0f;
    float lockOnSwitchCooldownTimer = 0f;

    protected override void Start()
    {
        base.Start();

        playerInput = PlayerInputManager.Instance;

        if (character.inventoryManager is not PlayerInventoryManager inventory) return;
        if (character.equipmentManager is not PlayerEquipmentManager equipment) return;
    }

    protected override void Update()
    {
        if (character.statusManager.isDead) return;
        base.Update();

        if (character.locomotionManager is not PlayerLocomotionManager locomotion) return;
        if (character.equipmentManager is not PlayerEquipmentManager equipment) return;
        if (character.animatorManager is not PlayerAnimatorManager animator) return;
        if (character.combatManager is not PlayerCombatManager combat) return;

        Debug(locomotion, equipment, animator, combat);

        HandleLocomotion(locomotion, equipment, animator, combat);
        HandleWeaponActions(locomotion, equipment, animator, combat);
        HandleGeneralActions(locomotion, equipment, animator, combat);
        UnequipWeaponAfterDelay(locomotion, equipment, animator, combat);
        HandleLockOn(locomotion, equipment, animator, combat);
        HandleDodge(locomotion, combat, animator);

        HandleConsumablePotion(locomotion, equipment, animator, combat);
    }
    
    void HandleConsumablePotion(PlayerLocomotionManager locomotion, PlayerEquipmentManager equipment, PlayerAnimatorManager animator, PlayerCombatManager combat)
    {
        if (equipment.hasOffHandWeapon && equipment.offHandWeaponItem.itemName == "Potion")
        {
            Material[] materials = equipment.offHandWeaponObject.GetComponentsInChildren<MeshRenderer>().SelectMany(renderer => renderer.materials).ToArray();

            foreach (Material material in materials)
            {
                if (material.shader.name == "Shader Graphs/S_Props")
                {
                    if (character.statusManager is PlayerStatusManager playerStatusManager)
                    {
                        float manaPercent = playerStatusManager.currentMana / playerStatusManager.maxMana;
                        material.SetFloat("_Emissive_Power", manaPercent);
                    }
                }
            }
        }
    }

    protected override void UpdateMovementParameters()
    {
        base.UpdateMovementParameters();
        
        movementInput = PlayerInputManager.Instance.MoveInput;
        sprintInput = PlayerInputManager.Instance.SprintHeld;
        primaryActionInput = PlayerInputManager.Instance.PrimaryAction;
        secondaryActionInput = PlayerInputManager.Instance.SecondaryAction;

        cameraLookDirection = PlayerCamera.Instance.cameraObject.transform.forward;
        cameraFront = cameraLookDirection;
        cameraRight = Vector3.Cross(Vector3.up, cameraFront).normalized;
        cameraFront.y = 0f; cameraFront.Normalize();
        cameraRight.y = 0f; cameraRight.Normalize();
        desiredMovementDirection = cameraFront * movementInput.y + cameraRight * movementInput.x;
        desiredMovementDirection.Normalize();
    }

    void HandleLocomotion(PlayerLocomotionManager locomotion, PlayerEquipmentManager equipment, PlayerAnimatorManager animator, PlayerCombatManager combat)
    {
        locomotion.HandleGravity();
        if (animator.isPerformingAction) animator.UpdateAnimatorMovementParameters(0, 0, 0);

        const float passiveStaminaRegen = 3f;
        if (!animator.isPerformingAction && moveAmount <= 0.5f)
        {
            character.statusManager.RegainStamina(passiveStaminaRegen * Time.deltaTime);
        }

        // Dialogue handling (overrides movement and rotation)
        if (dialogueObject != null && dialogueObject.activeSelf)
        {
            locomotion.HandleFaceDirection(dialogueObject.transform.position - transform.position);
            animator.UpdateAnimatorMovementParameters(0, 0, 0);
            return;
        }

        // Movement and rotation
        const float sprintStaminaCost = 1f;
        if (locomotion.canMove)
        {
            if (moveAmount > 0.5f)
                character.statusManager.LoseStamina(sprintStaminaCost * Time.deltaTime);
            if (character.statusManager.currentStamina < 0.1f && moveAmount > 0.5f)
                moveAmount = 0.5f;

            if (combat.isLockedOn)
            {
                locomotion.HandleLockedOnMovement(desiredMovementDirection, moveAmount);
            }
            else
            {
                locomotion.HandleGroundedMovement(desiredMovementDirection, moveAmount);
            }
        }

        if (locomotion.canRotate)
        {
            if (combat.isLockedOn)
            {
                locomotion.HandleFaceDirection(lockOnTargetDirection);
            }
            else
            {
                locomotion.HandleFaceDirection(desiredMovementDirection);
            }
        }
    }

    void HandleLockOn(PlayerLocomotionManager locomotion, PlayerEquipmentManager equipment, PlayerAnimatorManager animator, PlayerCombatManager combat)
    {
        if (lockOnSwitchCooldownTimer > 0f)
        {
            lockOnSwitchCooldownTimer -= Time.deltaTime;
        }

        if (playerInput.LockOnPressed)
        {
            lockOnSwitchAccum = 0f;
            lockOnSwitchReleased = false;

            if (combat.isLockedOn)
            {
                combat.SetLockOnTarget(null);
                return;
            }

            combat.SetLockOnTarget(combat.FindTarget());

            if (combat.lockedOnTarget == null) return;

            if (animator.isInAnimation) return;
            if (equipment.isMainHandWeaponHeld) return;
            if (equipment.mainHandWeaponItem == null) return;

            animator.PlayTargetCosmeticAnimation(animator.animations.EquipMainWeapon, interruptOverrideLayer: false);
            return;
        }

        if (!combat.isLockedOn)
        {
            lockOnSwitchAccum = 0f;
            lockOnSwitchReleased = true;
            lockOnSwitchCooldownTimer = 0f;
            return;
        }
        
        if (Vector3.Distance(transform.position, combat.lockedOnTarget.transform.position) > combat.lockOnRadius ||
            !combat.lockedOnTarget.combatManager.canBeLockedOn)
        {
            combat.SetLockOnTarget(null);
            return;
        }

        float lookX = playerInput.LookInput.x;
        const float releaseThreshold = 5f;
        const float switchThreshold = 20f;
        const float switchCooldown = 0.5f;

        if (Mathf.Abs(lookX) <= releaseThreshold)
        {
            lockOnSwitchAccum = 0f;
            lockOnSwitchReleased = true;
            return;
        }

        if (!lockOnSwitchReleased) return;
        if (lockOnSwitchCooldownTimer > 0f) return;

        if (lockOnSwitchAccum != 0f && Mathf.Sign(lookX) != Mathf.Sign(lockOnSwitchAccum))
        {
            lockOnSwitchAccum = 0f;
        }

        lockOnSwitchAccum += lookX;

        if (Mathf.Abs(lockOnSwitchAccum) < switchThreshold) return;

        float direction = Mathf.Sign(lockOnSwitchAccum);
        CharacterManager nextTarget = combat.FindTargetRelativeToLockOn(PlayerCamera.Instance.cameraObject, direction);

        if (nextTarget != null)
        {
            combat.SetLockOnTarget(nextTarget);
            lockOnSwitchCooldownTimer = switchCooldown;
        }

        lockOnSwitchAccum = 0f;
        lockOnSwitchReleased = false;
    }

    void HandleDodge(PlayerLocomotionManager locomotion, PlayerCombatManager combat, PlayerAnimatorManager animator)
    {
        if (!playerInput.DodgePressed) return;
        
        // Dialogue handling
        if (dialogueObject != null && dialogueObject.activeSelf) return;

        const float dodgeStaminaCost = 1f;
        if (combat.canDodge && character.statusManager.currentStamina > dodgeStaminaCost)
        {
            timeSinceMainHandWeaponActivity = 0f;
            if (combat.isLockedOn)
            {
                locomotion.PeformDodge(desiredMovementDirection, moveAmount);
                //character.statusManager.LoseStamina(dodgeStaminaCost);
            }
            else
            {
                locomotion.PeformDodge(characterForward, moveAmount);
                //character.statusManager.LoseStamina(dodgeStaminaCost);
            }
        }
    }

    void HandleWeaponActions(PlayerLocomotionManager locomotion, PlayerEquipmentManager equipment, PlayerAnimatorManager animator, PlayerCombatManager combat)
    {

        // Block
        animator.UpdateAnimatorBlockParameters(playerInput.BlockHeld);
        if (playerInput.BlockPressed && combat.canBlock && equipment.mainHandWeaponItem != null && !combat.isStunned)
        {
            // Dialogue handling
            if (dialogueObject != null && dialogueObject.activeSelf) return;
            if (!equipment.isMainHandWeaponHeld)
            {
                timeSinceMainHandWeaponActivity = 0f;
            }

            if (!equipment.isMainHandWeaponHeld)
            {
                animator.PlayTargetBlockAnimation(animator.animations.BlockStartUnequipped);
            }
            else
            {
                animator.PlayTargetBlockAnimation(animator.animations.BlockStart);
            }
        }

        // Dialogue handling
        if (dialogueObject != null && dialogueObject.activeSelf) return;
        
        // Light Attack
        if (primaryActionInput && combat.canAttack && equipment.mainHandWeaponItem != null && !combat.isStunned)
        {
            if (!equipment.isMainHandWeaponHeld)
            {
                equipment.EquipMainHandWeapon();
                timeSinceMainHandWeaponActivity = 0f;
            }
            
            if (equipment.isMainHandWeaponHeld)
            {
                if (!combat.HandleTargetExecution(combat.lockedOnTarget))
                {
                    combat.PerformWeaponAction(equipment.mainHandWeaponManager.weaponItem.primaryAction, equipment.mainHandWeaponManager);
                }
            }

            // TESTING
            //character.animatorManager.PlayTargetAnimation("Attack1", crossFadeDuration: 0f, animatorLayer: 2);
        }

        if (secondaryActionInput && combat.canAttack && equipment.mainHandWeaponItem != null && !combat.isStunned)
        {
            if (!equipment.isMainHandWeaponHeld)
            {
                equipment.EquipMainHandWeapon();
                timeSinceMainHandWeaponActivity = 0f;
            }
            
            if (equipment.isMainHandWeaponHeld)
            {
                animator.PlayTargetAttackAnimation(animator.animations.DashAttack);
            }
        }
    }
    
    void HandleGeneralActions(PlayerLocomotionManager locomotion, PlayerEquipmentManager equipment, PlayerAnimatorManager animator, PlayerCombatManager combat)
    {
        // Interaction
        if (playerInput.InteractPressed && !animator.isPerformingAction)
        {
            if (character is not PlayerManager player) return;

            player.interactionManager.Interact();
        }

        // Dialogue handling
        if (dialogueObject != null && dialogueObject.activeSelf) return;

        
        // Item usage
        if (playerInput.UseItemPressed)
        {
            if (combat.canHeal && !combat.isStunned && equipment.offHandWeaponItem != null)
            {
                if (character.statusManager is PlayerStatusManager playerStatusManager)
                {
                    if (playerStatusManager.currentMana >= 5)
                    {
                        animator.PlayTargetAnimation(animator.animations.Heal, canMove: true, canRotate: true, interruptOverrideLayer: false);
                    }
                }
            }
        }
    }

    void UnequipWeaponAfterDelay(PlayerLocomotionManager locomotion, PlayerEquipmentManager equipment, PlayerAnimatorManager animator, PlayerCombatManager combat)
    {
        bool hasMainHandWeaponHeld = equipment.isMainHandWeaponHeld && equipment.mainHandWeaponItem != null;
        animator.animator.SetFloat(animator.animations.inCombatHash, hasMainHandWeaponHeld ? 1f : 0f);

        if (!hasMainHandWeaponHeld)
        {
            timeSinceMainHandWeaponActivity = 0f;
            return;
        }

        if (combat.isLockedOn)
        {
            timeSinceMainHandWeaponActivity = 0f;
            return;
        }

        timeSinceMainHandWeaponActivity += Time.deltaTime;

        const float unequipDelay = 3f;
        if (timeSinceMainHandWeaponActivity < unequipDelay) return;
        if (animator.isInAnimation) return;

        timeSinceMainHandWeaponActivity = 0f;
        animator.PlayTargetCosmeticAnimation(animator.animations.UnequipMainWeapon, interruptOverrideLayer: false);
    }

    void Debug(PlayerLocomotionManager locomotion, PlayerEquipmentManager equipment, PlayerAnimatorManager animator, PlayerCombatManager combat)
    {
        if (playerInput.DebugAction1)
        {
            
        }
        if (playerInput.DebugAction2)
        {
            
        }

        if (playerInput.DebugAction3)
        {
            
        }

        if (playerInput.DebugAction4)
        {
            Time.timeScale = 0.1f;
        }

        if (playerInput.DebugAction5)
        {
            Time.timeScale = 1f;
        }
    }
}
