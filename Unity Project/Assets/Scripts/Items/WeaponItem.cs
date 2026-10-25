using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeaponItem : Item
{
    // animator controller override
    [Header("Model & Effects")]
    public GameObject model;
    public AudioClip equipSound;
    public AudioClip unequipSound;
    public AudioClip swingSound;
    public AudioClip parrySound;
    public AudioClip blockSound;
    public AudioClip hitSound;
    public GameObject hitEffectPrefab;
    public GameObject blockEffectPrefab;
    public GameObject parryEffectPrefab;
    [Header("Requirements")]
    public int strengthRequirement = 0;
    public int dexterityRequirement = 0;

    [Header("Damage and Effects")]
    public DamageData damageData = new DamageData(physical: 1, poise: 1);
    public float guardAbsorption = 1;
    public float deflectionResistance = 1;
    public List<CharacterEffect> onHitEffects = new List<CharacterEffect>();

    [Header("Multipliers")]
    public float lightAttackModifier = 1;
    public float heavyAttackModifier = 1.5f;
    [Header("Weapon Actions")]
    public WeaponItemAction primaryAction;
    public WeaponItemAction secondaryAction;
}
