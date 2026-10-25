using System.Collections;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterCombatManager : MonoBehaviour
{
    protected CharacterManager character;
    public CharacterManager lockedOnTarget { get; protected set; }
    public Transform lockOnTransform { get; protected set; }
    protected Dictionary<string, HitBox> hitboxes = new Dictionary<string, HitBox>();
    protected Dictionary<string, HurtBox> hurtBoxes = new Dictionary<string, HurtBox>();

    [Min(0)] public float lockOnRadius = 10;
    [Range(0, 360)] public float lockOnFOV = 80;
    public bool isLockedOn => lockedOnTarget != null;
    public bool canBeLockedOn = true;
    public bool canBeExecuted = true;
    public bool isAttacking = false;
    public bool isBlocking = false;
    public bool isParrying = false;
    public bool isDodging = false;
    public bool isHealing = false;
    public bool isHitReacting = false;
    public bool isStunned = false;
    public bool isExposed = false;
    public bool hasInvincibility = false;
    public bool hasHyperArmor = false;
    public bool isInCombat = false;
    [Min(0)] public int comboIndex = 0;
    public bool canPerformNextComboAttack = false;
    public bool canAttack = true;
    public bool canBlock = true;
    public bool canDodge = true;
    public bool canHeal = true;

    protected virtual void Awake()
    {
        character = GetComponent<CharacterManager>();
        lockOnTransform = GetComponentInChildren<LockOnTransform>()?.transform;
    }
    protected virtual void Start()
    {
        character.statusManager.OnPoiseBroken += HandleStunnedAnimation;
    }

    protected virtual void OnDisable()
    {
        character.statusManager.OnPoiseBroken -= HandleStunnedAnimation;
    }

    public virtual void PerformWeaponAction(WeaponItemAction weaponAction, WeaponManager weapon)
    {
        weaponAction.PerformAction(character, weapon);
    }

    public void HandleHitBoxRegistration(HitBox hitbox, bool added)
    {
        if (hitbox == null) return;
        if (!hitbox.transform.IsChildOf(transform)) return;

        if (added)
            if (hitboxes.ContainsKey(hitbox.hitboxName)) Debug.LogWarning($"Hurtbox with name [{hitbox.hitboxName}] already exists");
            else hitboxes[hitbox.hitboxName] = hitbox;
        else
            hitboxes.Remove(hitbox.hitboxName);
    }

    public void HandleHurtBoxRegistration(HurtBox hurtbox, bool added)
    {
        if (hurtbox == null) return;
        if (!hurtbox.transform.IsChildOf(transform)) return;

        if (added)
            if (hurtBoxes.ContainsKey(hurtbox.hurtboxName)) Debug.LogWarning($"Hurtbox with name [{hurtbox.hurtboxName}] already exists");
            else hurtBoxes[hurtbox.hurtboxName] = hurtbox;
        else
            hurtBoxes.Remove(hurtbox.hurtboxName);
    }

    public void ToggleHitBoxDetection(string parameters) // Obsolete, use ToggleHitBoxDetection(string name, bool enabled) instead for better performance
    {
        var parser = new AnimationEventStringParser(parameters);

        //Special case "ALL"
        string keywordAll = "all";
        if (parser.KeyValuePairs.ContainsKey(keywordAll))
        {
            string enabled = parser.KeyValuePairs[keywordAll].ToLower();
            foreach (var hb in hitboxes.Values)
            {
                switch (enabled)
                {
                    case "on": hb.EnableHitBox(); break;
                    case "off": hb.DisableHitBox(); break;
                    default: Debug.LogWarning($"Value '{enabled}' for hitboxes can only be [on/off]."); break;
                }
            }
            return;
        }

        foreach (var kvp in parser.KeyValuePairs)
        {
            string name = kvp.Key;
            string enabled = kvp.Value.ToLower();

            if (!hitboxes.TryGetValue(name, out HitBox hitbox))
            {
                Debug.LogWarning($"[{name}] hitbox not found on {gameObject.name}", this);
                continue;
            }

            switch (enabled)
            {
                case "on": hitbox.EnableHitBox(); break;
                case "off": hitbox.DisableHitBox(); break;
                default: Debug.LogWarning($"Value '{enabled}' for hitbox '{name}' can only be [on/off]."); break;
            }
        }
    }

    public void ToggleHurtBoxDetection(string parameters) // Obsolete, use ToggleHurtBoxDetection(string name, bool enabled) instead for better performance
    {
        var parser = new AnimationEventStringParser(parameters);

        //Special case "ALL"
        string keywordAll = "all";
        if (parser.KeyValuePairs.ContainsKey(keywordAll))
        {
            string enabled = parser.KeyValuePairs[keywordAll].ToLower();
            foreach (var hb in hurtBoxes.Values)
            {
                switch (enabled)
                {
                    case "on": hb.EnableHurtBox(); break;
                    case "off": hb.DisableHurtBox(); break;
                    default: Debug.LogWarning($"Value '{enabled}' for hurtboxes can only be [on/off]."); break;
                }
            }
            return;
        }

        foreach (var kvp in parser.KeyValuePairs)
        {
            string name = kvp.Key;
            string enabled = kvp.Value.ToLower();

            if (!hurtBoxes.TryGetValue(name, out HurtBox hurtbox))
            {
                Debug.LogWarning($"[{name}] hurtbox not found on {gameObject.name}", this);
                continue;
            }

            switch (enabled)
            {
                case "on": hurtbox.EnableHurtBox(); break;
                case "off": hurtbox.DisableHurtBox(); break;
                default: Debug.LogWarning($"Value '{enabled}' for hurtbox '{name}' can only be [on/off]."); break;
            }
        }
    }

    public void ToggleHitBoxDetection(string name, bool enabled)
    {
        if (!hitboxes.TryGetValue(name, out HitBox hitbox))
        {
            Debug.LogWarning($"[{name}] hitbox not found on {gameObject.name}", this);
            return;
        }

        if (enabled) hitbox.EnableHitBox();
        else hitbox.DisableHitBox();
    }

    public void ToggleHurtBoxDetection(string name, bool enabled)
    {
        if (!hurtBoxes.TryGetValue(name, out HurtBox hurtbox))
        {
            Debug.LogWarning($"[{name}] hurtbox not found on {gameObject.name}", this);
            return;
        }

        if (enabled) hurtbox.EnableHurtBox();
        else hurtbox.DisableHurtBox();
    }

    public HitBox GetHitBox(string name)
    {
        if (!hitboxes.TryGetValue(name, out HitBox hitbox))
        {
            Debug.LogWarning($"[{name}] hitbox not found on {gameObject.name}", this);
            return null;
        }
        return hitbox;
    }

    public HurtBox GetHurtBox(string name)
    {
        if (!hurtBoxes.TryGetValue(name, out HurtBox hurtbox))
        {
            Debug.LogWarning($"[{name}] hurtbox not found on {gameObject.name}", this);
            return null;
        }
        return hurtbox;
    }

    public List<HitBox> GetAllHitBoxes()
    {
        List<HitBox> activeHitBoxes = new List<HitBox>();
        foreach (var hitbox in hitboxes.Values)
        {
            activeHitBoxes.Add(hitbox);
        }
        return activeHitBoxes;
    }
    
    public List<HurtBox> GetAllHurtBoxes()
    {
        List<HurtBox> activeHurtBoxes = new List<HurtBox>();
        foreach (var hurtbox in hurtBoxes.Values)
        {
            activeHurtBoxes.Add(hurtbox);
        }
        return activeHurtBoxes;
    }

    public virtual void HandleParryAttack(DamageData damageData)
    {
        Vector3 direction = damageData.attacker.transform.position - character.transform.position;
        direction.y = 0; direction.Normalize();
        //character.transform.rotation = Quaternion.LookRotation(direction);

        int randomAnim = UnityEngine.Random.Range(0, 2);

        CharacterAnimatorManager animator = character.animatorManager;
        switch (randomAnim)
        {
            case 0: animator.PlayTargetBlockAnimation(animator.animations.ParryLeft, crossFadeDuration: 0f); break;
            case 1: animator.PlayTargetBlockAnimation(animator.animations.ParryRight, crossFadeDuration: 0f); break;
        }

        //damageData.attacker.animatorManager?.PlayTargetHitAnimation("HeavyHit_forward_01");
        damageData.attacker.statusManager.LosePoise(1);

        CharacterSoundFXManager soundFXManager = character.soundFXManager;
        soundFXManager.PlaySoundEffect(soundFXManager.parrySound);

        CharacterVisualFXManager visualFXManager = character.visualFXManager;
        visualFXManager.PlayVisualEffectWorldSpace(visualFXManager.parryEffect, damageData.contactPoint, Vector3.zero);
    }

    public virtual void HandleBlockAttack(DamageData damageData)
    {
        Vector3 direction = damageData.attacker.transform.position - character.transform.position;
        direction.y = 0; direction.Normalize();
        character.transform.rotation = Quaternion.LookRotation(direction);

        CharacterAnimatorManager animator = character.animatorManager;
        animator.PlayTargetHitAnimation(animator.animations.BlockHit, crossFadeDuration: 0f, isBlocking: true, isHitReacting: true, isStunned: false, isExposed: false);
        //damageData.attacker.animatorManager?.PlayTargetHitAnimation("Hit_forward_01");

        const float absorptionRatio = 0.5f;
        
        if (character.statusManager.currentPoise - damageData.poise * absorptionRatio > 0)
        {
            CharacterSoundFXManager soundFXManager = character.soundFXManager;
            soundFXManager.PlaySoundEffect(soundFXManager.blockSound);
        }
        character.statusManager.InflictDamage(damageData * absorptionRatio);

        CharacterVisualFXManager visualFXManager = character.visualFXManager;
        visualFXManager.PlayVisualEffectWorldSpace(visualFXManager.blockEffect, damageData.contactPoint, Vector3.zero);
    }

    public virtual void HandleReceiveDamage(DamageData damageData)
    {
        DamageData resolvedDamage = new DamageData() { physical = damageData.physical, poise = 0, attacker = damageData.attacker, damageType = damageData.damageType };
        character.statusManager.InflictDamage(resolvedDamage);

        CharacterSoundFXManager soundFXManager = character.soundFXManager;
        soundFXManager.PlaySoundEffect(soundFXManager.damageSound);
        
        if (damageData.physical + damageData.magical > 0)
        {
            CharacterVisualFXManager visualFXManager = character.visualFXManager;
            Vector3 offset = character.transform.position + new Vector3(0, 1, 0);
            Vector3 flatDirection = new Vector3(offset.x - damageData.contactPoint.x, 0, offset.z - damageData.contactPoint.z).normalized;
            Vector3 direction = Quaternion.LookRotation(flatDirection).eulerAngles;
            visualFXManager.PlayVisualEffectWorldSpace(visualFXManager.damageEffect, offset, direction);
            print("rahh");
        }

        if (hasHyperArmor && !damageData.breaksHyperArmor) return;
        if (isStunned) return;

        CharacterAnimatorManager animator = character.animatorManager;
        string targetAnimation = false // TODO: Add more directional hit reactions
            ? animator.animations.HeavyHitForward
            : animator.animations.HitForward;

        animator.PlayTargetHitAnimation(targetAnimation, crossFadeDuration: 0f, isHitReacting: true, isStunned: false);
    }

    public void HandleStunnedAnimation()
    {
        if (isStunned) return;

        hasHyperArmor = false;

        CharacterAnimatorManager animator = character.animatorManager;
        animator.PlayTargetHitAnimation(animator.animations.BlockHitBreak, crossFadeDuration: 0f, isHitReacting: true, isStunned: true, isExposed: true, forceInterruption: true);

        CharacterSoundFXManager soundFXManager = character.soundFXManager;
        soundFXManager.PlaySoundEffect(soundFXManager.poiseBreakSound, randomPitchRange: 0f);
    }

    public bool HandleTargetExecution(CharacterManager target)
    {
        if (target == null) return false;
        if (!target.combatManager.isStunned || !target.combatManager.canBeExecuted) return false;
        if (character.animatorManager.isPerformingAction) return false;
        if (Vector3.Distance(character.transform.position, target.transform.position) > 3) return false;

        Vector3 targetPosition = target.transform.position + target.transform.forward * 0.1f;
        Vector3 delta = targetPosition - character.transform.position;
        character.characterController.Move(delta);

        Vector3 characterDirection = (target.transform.position - character.transform.position).normalized;
        characterDirection.y = 0; characterDirection.Normalize();
        character.transform.rotation = Quaternion.LookRotation(characterDirection);

        CharacterAnimatorManager animator = character.animatorManager;
        animator.PlayTargetAnimation(animator.animations.ExecuteTarget, applyRootMotion: false, canBeInterrupted: false, hasInvincibility: true, forceInterruption:true);

        Vector3 targetDirection = character.transform.position - target.transform.position;
        targetDirection.y = 0; targetDirection.Normalize();
        target.transform.rotation = Quaternion.LookRotation(targetDirection);

        CharacterAnimatorManager targetAnimator = target.animatorManager;
        targetAnimator.PlayTargetAnimation(targetAnimator.animations.GetExecuted, canBeInterrupted: false, isExposed: true);

        target.combatManager.isExposed = false;

        return true;
    }

    public virtual CharacterManager FindTarget()
    {
        if (character == null) return null;

        Collider[] targetColliders = Physics.OverlapSphere(
            character.transform.position,
            lockOnRadius,
            GameManager.Instance.characterLayer
        );

        CharacterManager closestTarget = null;
        float closest = float.MaxValue;

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Vector3 offset = new Vector3(0, 2, 0);
            CharacterManager target = targetColliders[i].GetComponent<CharacterManager>();
            if (target == null || target.statusManager.isDead) continue;
            if (target == character) continue;
            if (target.combatManager.canBeLockedOn == false) continue;
            if (target.transform.root == character.transform.root) continue;
            if (target.characterGroup == character.characterGroup) continue;
            if (character.controller.characterForward.magnitude < 0.01f) continue;

            Vector3 direction = (target.transform.position + offset) - (character.transform.position + offset);
            direction.y = 0;
            direction.Normalize();
            float distance = Vector3.Distance(target.transform.position + offset, character.transform.position + offset);
            float angle = Vector3.Angle(character.controller.characterForward, direction);

            if (angle > lockOnFOV / 2) continue;
            if (distance > lockOnRadius) continue;

            RaycastHit hit;
            if (Physics.Linecast(
                target.transform.position + offset,
                character.transform.position + offset,
                out hit,
                GameManager.Instance.environmentLayer)) continue;

            if (distance < closest)
            {
                closest = distance;
                closestTarget = target;
            }
        }
        return closestTarget;
    }

    private void FindAndSetLockOnTarget()
    {
        SetLockOnTarget(FindTarget());
    }
    
    public void SetLockOnTarget(CharacterManager target)
    {
        if (lockedOnTarget != null)
        {
            lockedOnTarget.statusManager.OnCharacterDeath -= FindAndSetLockOnTarget;
            lockedOnTarget.GetComponentInChildren<CharacterUIStatusManager>()?.SetLockOnIndicator(false);
        }
        
        lockedOnTarget = target;

        if (lockedOnTarget != null)
        {
            lockedOnTarget.statusManager.OnCharacterDeath += FindAndSetLockOnTarget;
            lockedOnTarget.GetComponentInChildren<CharacterUIStatusManager>()?.SetLockOnIndicator(true);
        }
    }
}
