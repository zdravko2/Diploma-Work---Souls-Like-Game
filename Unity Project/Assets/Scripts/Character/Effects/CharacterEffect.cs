using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public abstract class CharacterEffect : ScriptableObject
{
    [Header("Metadata")]
    public string effectName;
    public Sprite icon;
    public bool displayIcon = true;

    public virtual void OnStartEffect(CharacterManager target) { }

    public virtual void OnTickEffect(CharacterManager target, float deltaTime) { }

    public virtual void OnEndEffect(CharacterManager target) { }
}

public abstract class InstantCharacterEffect : CharacterEffect
{
    public static InstantMeleeWeaponDamageEffect BasicHitEffect(DamageData damage)
    {
        var instance = ScriptableObject.CreateInstance<InstantMeleeWeaponDamageEffect>();
        instance.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        instance.damage = damage;
        return instance;
    }
}

public abstract class TimedCharacterEffect : CharacterEffect
{
    [Header("Timed Settings")]
    public float duration = 5f;
    public float remainingDuration = 0f;

    [Header("Settings")]
    public bool stackable = false;
}

public abstract class StaticCharacterEffect : CharacterEffect
{
    [Header("Settings")]
    public bool stackable = false;
}