using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HurtBox : MonoBehaviour
{
    public string hurtboxName = "Unnamed HurtBox";
    private Collider hurtCollider;
    public CharacterManager owner;

    void Awake()
    {
        owner = GetComponentInParent<CharacterManager>();
        Reset();

        Debug.Assert(hurtboxName != "" && hurtboxName != "Unnamed HurtBox", $"HurtBox on gameobject [{gameObject.name}] has no name set!", this);

        EnableHurtBox();
    }


    void Reset()
    {
        hurtCollider = GetComponent<Collider>();
        hurtCollider.enabled = true;
        hurtCollider.isTrigger = true;
        hurtboxName = hurtboxName != "" && hurtboxName != "Unnamed HurtBox" ? hurtboxName : gameObject.name;
        gameObject.layer = LayerMask.NameToLayer("HurtBox");
    }

    void Start()
    {
        owner.combatManager.HandleHurtBoxRegistration(this, true);
        owner.statusManager.OnCharacterDeath += DisableHurtBox;
    }

    void OnDisable()
    {
        owner.combatManager.HandleHurtBoxRegistration(this, false);
        owner.statusManager.OnCharacterDeath -= DisableHurtBox;
    }

    public void ReceiveDamageAndEffects(DamageData damage, List<CharacterEffect> effects)
    {
        if (owner.combatManager.isParrying)
        {
            owner.combatManager.HandleParryAttack(damage);
        }
        else if (owner.combatManager.isBlocking)
        {
            owner.combatManager.HandleBlockAttack(damage);
        }
        else
        {
            owner.combatManager.HandleReceiveDamage(damage);
            foreach (CharacterEffect effect in effects)
            {
                owner.effectsManager.ApplyEffect(effect);
            }
        }
    }

    public void EnableHurtBox()
    {
        hurtCollider.enabled = true;
    }

    public void DisableHurtBox()
    {
        hurtCollider.enabled = false;
    }
}
