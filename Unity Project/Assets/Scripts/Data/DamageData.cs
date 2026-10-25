using UnityEngine;
using static DamageData;

[System.Serializable]
public class DamageData
{
    public DamageType damageType = DamageType.Special;
    [Min(0)] public float physical = 0;
    [Min(0)] public float magical = 0;
    [Min(0)] public float poise = 0;
    [Min(0)] public float stamina = 0;
    [Min(0)] public float knockbackStrength = 0;
    public bool breaksHyperArmor = false;
    [HideInInspector] public Vector3 contactPoint;
    [HideInInspector] public CharacterManager attacker;

    public DamageData(
        DamageType damageType = DamageType.Special,
        float physical = 0,
        float magical = 0,
        float poise = 0,
        float stamina = 0,
        float knockbackStrength = 0,
        bool breaksHyperArmor = false,
        Vector3 contactPoint = new Vector3(),
        CharacterManager attacker = null)
    {
        this.damageType = damageType;
        this.physical = physical;
        this.magical = magical;
        this.poise = poise;
        this.stamina = stamina;
        this.knockbackStrength = knockbackStrength;
        this.breaksHyperArmor = breaksHyperArmor;
        this.contactPoint = contactPoint;
        this.attacker = attacker;
    }

    public static DamageData operator *(DamageData data, float multiplier)
    {
        return new DamageData(
            data.damageType,
            data.physical * multiplier,
            data.magical * multiplier,
            data.poise * multiplier,
            data.stamina * multiplier,
            data.knockbackStrength,
            data.breaksHyperArmor,
            data.contactPoint,
            data.attacker
        );
    }

    public static DamageData operator *(float multiplier, DamageData data)
    {
        return data * multiplier;
    }

    public static DamageData operator *(DamageData data, DamageData multiplier)
    {
        return new DamageData(
            data.damageType,
            data.physical * multiplier.physical,
            data.magical * multiplier.magical,
            data.poise * multiplier.poise,
            data.stamina * multiplier.stamina,
            data.knockbackStrength * multiplier.knockbackStrength,
            data.breaksHyperArmor,
            data.contactPoint,
            data.attacker
        );
    }
}

