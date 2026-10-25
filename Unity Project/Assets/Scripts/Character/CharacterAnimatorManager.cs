using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class CharacterAnimatorManager : MonoBehaviour
{
    CharacterManager character;
    [HideInInspector] public Animator animator;
    public bool applyRootMotion = false;
    public bool canBeInterrupted = true;
    public bool isPerformingAction;
    public bool isInAnimation;
    [Min(0)] float directionDampTime = 0.1f;
    [Min(0)] float moveAmountDampTime = 0.2f;
    public float rootMotionMultiplier = 1;

    public AnimationReferences animations;

    public class AnimationReferences
    {
        public readonly string Heal = "Drink";
        public readonly string Dodge = "Dodge";
        public readonly string ParryLeft = "Parry Left";
        public readonly string ParryRight = "Parry Right";
        public readonly string HitForward = "Hit Forward";
        public readonly string HeavyHitForward = "Heavy Hit Forward";
        public readonly string BlockStart = "Block Start";
        public readonly string BlockStartUnequipped = "Block Start Unequipped";
        public readonly string BlockHit = "Block Hit";
        public readonly string BlockHitBreak = "Block Hit Break";
        public readonly string HitDeath = "Hit Death";
        public readonly string ExecuteTarget = "Execute Target";
        public readonly string GetExecuted = "Get Executed";
        public readonly string EquipMainWeapon = "Equip Main Weapon";
        public readonly string UnequipMainWeapon = "Unequip Main Weapon";
        public readonly string DashAttack = "Dash Attack";
        public readonly string GrabLow = "Grab Low";
        public readonly string GrabMedium = "Grab Medium";
        public readonly string GrabHigh = "Grab High";
        public readonly string Interact = "Interact";

        public readonly int moveHorizontalHash = Animator.StringToHash("MoveHorizontal");
        public readonly int moveVerticalHash = Animator.StringToHash("MoveVertical");
        public readonly int moveAmountHash = Animator.StringToHash("MoveAmount");
        public readonly int inCombatHash = Animator.StringToHash("InCombat");
        public readonly int blockingHash = Animator.StringToHash("IsBlocking");
        public readonly int isDeadHash = Animator.StringToHash("IsDead");
        
        public readonly string baseLayerInterruptTrigger = "InteruptBaseLayer";
        public readonly string overrideLayerInterruptTrigger = "InteruptOverrideLayer";
    }

    protected virtual void Awake()
    {
        character = GetComponent<CharacterManager>();
        animator = GetComponent<Animator>();
        animations = CreateAnimationReferences();
    }
    
    protected virtual AnimationReferences CreateAnimationReferences()
    {
        return new AnimationReferences();
    }

    private void OnAnimatorMove()
    {
        if (applyRootMotion)
        {
            Vector3 velocity = animator.deltaPosition * rootMotionMultiplier;
            
            if (character.characterController.enabled)
            {
                character.characterController.Move(velocity);
            }
            
            character.transform.rotation *= animator.deltaRotation;
        }

        NavMeshAgent agent = character.controller.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.nextPosition = character.transform.position;
        }
    }

    public void UpdateAnimatorMovementParameters(float horizontalValue, float verticalValue, float moveAmountValue)
    {
        animator.SetFloat(animations.moveHorizontalHash, horizontalValue, directionDampTime, Time.deltaTime);
        animator.SetFloat(animations.moveVerticalHash, verticalValue, directionDampTime, Time.deltaTime);
        animator.SetFloat(animations.moveAmountHash, moveAmountValue, moveAmountDampTime, Time.deltaTime);
    }

    public void UpdateAnimatorBlockParameters(bool isBlocking)
    {
        animator.SetBool(animations.blockingHash, isBlocking);
    }

    public void UpdateAnimatorStatusParameters(bool isDead)
    {
        animator.SetBool(animations.isDeadHash, isDead);
    }

    public virtual void InterruptAnimationEvents()
    {
        AnimatorStateEventManager.InterruptBeforeTransition(animator, -1);
    }

    public virtual void PlayTargetCosmeticAnimation(
        string targetAnimation,
        float crossFadeDuration = 0.2f,
        int animatorLayer = -1,
        float normalizedTimeOffset = 0f,
        float normalizedTransitionTime = 0.0f,
        bool interruptBaseLayer = true,
        bool interruptOverrideLayer = true,
        bool canBeInterrupted = true,
        bool applyRootMotion = true,
        bool forceInterruption = false)
    {
        if (!this.canBeInterrupted && !forceInterruption) return;

        this.applyRootMotion = applyRootMotion;
        this.canBeInterrupted = canBeInterrupted;
        this.isInAnimation = true;

        if (interruptBaseLayer) animator.SetTrigger(animations.baseLayerInterruptTrigger);
        if (interruptOverrideLayer) animator.SetTrigger(animations.overrideLayerInterruptTrigger);

        InterruptAnimationEvents();
        animator.CrossFade(targetAnimation, crossFadeDuration, animatorLayer, normalizedTimeOffset, normalizedTransitionTime);
    }

    public virtual void PlayTargetAnimation(
        string targetAnimation,
        float crossFadeDuration = 0.2f,
        int animatorLayer = -1,
        float normalizedTimeOffset = 0f,
        float normalizedTransitionTime = 0.0f,
        bool interruptBaseLayer = true,
        bool interruptOverrideLayer = true,
        bool applyRootMotion = true,
        bool canBeInterrupted = true,
        bool forceInterruption = false,
        bool isPerformingAction = true,
        bool canMove = false,
        bool canRotate = false,
        bool canRun = false,
        bool canJump = false,
        bool canAttack = false,
        bool canBlock = false,
        bool canDodge = false,
        bool canHeal = false,
        bool isAttacking = false,
        bool isParrying = false,
        bool isBlocking = false,
        bool isDodging = false,
        bool isHealing = false,
        bool isHitReacting = false,
        bool isStunned = false,
        bool isExposed = false,
        bool hasInvincibility = false,
        bool hasHyperArmor = false,
        int comboOffset = 0)
    {
        if (!this.canBeInterrupted && !forceInterruption) return;

        this.applyRootMotion = applyRootMotion;
        this.canBeInterrupted = canBeInterrupted;
        this.isPerformingAction = isPerformingAction;
        this.isInAnimation = true;

        character.locomotionManager.canMove = canMove;
        character.locomotionManager.canRotate = canRotate;
        character.locomotionManager.canRun = canRun;
        character.locomotionManager.canJump = canJump;
        
        character.combatManager.canAttack = canAttack;
        character.combatManager.canBlock = canBlock;
        character.combatManager.canDodge = canDodge;
        character.combatManager.canHeal = canHeal;
        
        character.combatManager.isAttacking = isAttacking;
        character.combatManager.isParrying = isParrying;
        character.combatManager.isBlocking = isBlocking;
        character.combatManager.isDodging = isDodging;
        character.combatManager.isHealing = isHealing;
        character.combatManager.isHitReacting = isHitReacting;
        character.combatManager.isStunned = isStunned;
        character.combatManager.isExposed = isExposed;
        character.combatManager.hasInvincibility = hasInvincibility;
        character.combatManager.hasHyperArmor = hasHyperArmor;
        
        character.combatManager.comboIndex += comboOffset;
        character.combatManager.canPerformNextComboAttack = false;

        if (interruptBaseLayer) animator.SetTrigger(animations.baseLayerInterruptTrigger);
        if (interruptOverrideLayer) animator.SetTrigger(animations.overrideLayerInterruptTrigger);

        InterruptAnimationEvents();
        animator.CrossFade(targetAnimation, crossFadeDuration, animatorLayer, normalizedTimeOffset, normalizedTransitionTime);
    }

    public virtual void PlayTargetActionAnimation(
        string targetAnimation,
        float crossFadeDuration = 0.2f,
        int animatorLayer = -1,
        float normalizedTimeOffset = 0f,
        float normalizedTransitionTime = 0.0f,
        bool applyRootMotion = true,
        bool canBeInterrupted = true,
        bool canMove = false,
        bool canRotate = false,
        bool canRun = false,
        bool canJump = false,
        bool canHeal = false,
        bool isHealing = false,
        bool isDodging = false)
    {
        PlayTargetAnimation(
            targetAnimation,
            crossFadeDuration: crossFadeDuration,
            animatorLayer: animatorLayer,
            normalizedTimeOffset: normalizedTimeOffset,
            normalizedTransitionTime: normalizedTransitionTime,
            applyRootMotion: applyRootMotion,
            canBeInterrupted: canBeInterrupted,
            isPerformingAction: true,
            canMove: canMove,
            canRotate: canRotate,
            canRun: canRun,
            canJump: canJump,
            canHeal: canHeal,
            isHealing: isHealing,
            isDodging: isDodging);
    }

    public virtual void PlayTargetAttackAnimation(
        string targetAnimation,
        float crossFadeDuration = 0.2f,
        int animatorLayer = -1,
        float normalizedTimeOffset = 0f,
        float normalizedTransitionTime = 0.0f,
        bool applyRootMotion = true,
        bool canBeInterrupted = true,
        bool canMove = false,
        bool canRotate = false,
        bool canAttack = false,
        bool canBlock = false,
        bool canDodge = false,
        bool hasHyperArmor = false,
        int comboOffset = 1)
    {
        PlayTargetAnimation(
            targetAnimation,
            crossFadeDuration: crossFadeDuration,
            animatorLayer: animatorLayer,
            normalizedTimeOffset: normalizedTimeOffset,
            normalizedTransitionTime: normalizedTransitionTime,
            applyRootMotion: applyRootMotion,
            canBeInterrupted: canBeInterrupted,
            canMove: canMove,
            canRotate: canRotate,
            canAttack: canAttack,
            canBlock: canBlock,
            canDodge: canDodge,
            hasHyperArmor: hasHyperArmor,
            isAttacking: true,
            comboOffset: comboOffset);
    }

    public virtual void PlayTargetHitAnimation(
        string targetAnimation,
        float crossFadeDuration = 0.2f,
        int animatorLayer = -1,
        float normalizedTimeOffset = 0f,
        float normalizedTransitionTime = 0.0f,
        bool applyRootMotion = true,
        bool canBeInterrupted = true,
        bool forceInterruption = false,
        bool isPerformingAction = true,
        bool isBlocking = false,
        bool isHitReacting = true,
        bool isStunned = false,
        bool isExposed = false,
        bool hasInvincibility = false)
    {
        PlayTargetAnimation(
            targetAnimation,
            crossFadeDuration: crossFadeDuration,
            animatorLayer: animatorLayer,
            normalizedTimeOffset: normalizedTimeOffset,
            normalizedTransitionTime: normalizedTransitionTime,
            applyRootMotion: applyRootMotion,
            canBeInterrupted: canBeInterrupted,
            forceInterruption: forceInterruption,
            isPerformingAction: isPerformingAction,
            isBlocking: isBlocking,
            isHitReacting: isHitReacting,
            isStunned: isStunned,
            isExposed: isExposed,
            hasInvincibility: hasInvincibility);
    }

    public virtual void PlayTargetBlockAnimation(
    string targetAnimation,
    float crossFadeDuration = 0.2f,
    int animatorLayer = -1,
    float normalizedTimeOffset = 0f,
    float normalizedTransitionTime = 0.0f,
    bool applyRootMotion = true,
    bool canBeInterrupted = true,
    bool isPerformingAction = true)
    {
        PlayTargetAnimation(
            targetAnimation,
            crossFadeDuration: crossFadeDuration,
            animatorLayer: animatorLayer,
            normalizedTimeOffset: normalizedTimeOffset,
            normalizedTransitionTime: normalizedTransitionTime,
            applyRootMotion: applyRootMotion,
            canBeInterrupted: canBeInterrupted,
            isPerformingAction: isPerformingAction);
    }

    public void ResetAllAnimatorFlags()
    {
        character.animatorManager.applyRootMotion = true;

        character.animatorManager.isPerformingAction = false;
        character.animatorManager.isInAnimation = false;
        character.animatorManager.canBeInterrupted = true;

        character.locomotionManager.canMove = true;
        character.locomotionManager.canRotate = true;
        character.locomotionManager.canRun = true;
        character.locomotionManager.canJump = true;

        character.combatManager.canAttack = true;
        character.combatManager.canBlock = true;
        character.combatManager.canDodge = true;
        character.combatManager.canHeal = true;

        character.combatManager.isAttacking = false;
        character.combatManager.isParrying = false;
        character.combatManager.isBlocking = false;
        character.combatManager.isDodging = false;
        character.combatManager.isHealing = false;
        character.combatManager.isHitReacting = false;
        character.combatManager.isStunned = false;
        character.combatManager.isExposed = false;

        character.combatManager.comboIndex = 0;
        character.combatManager.canPerformNextComboAttack = false;

        character.combatManager.hasInvincibility = false;
        //character.combatManager.hasHyperArmor = false;
    }
}
