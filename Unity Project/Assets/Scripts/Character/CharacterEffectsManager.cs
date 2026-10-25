using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterEffectsManager : MonoBehaviour
{
    [Header("Active Effects (Read-Only)")]
    [SerializeField] private List<CharacterEffect> activeEffects = new List<CharacterEffect>();

    public event Action<CharacterEffect> OnEffectStarted;
    public event Action<CharacterEffect> OnEffectEnded;

    private CharacterManager character;

    private void Start()
    {
        character = GetComponent<CharacterManager>();
    }

    private void OnDisable()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i] is not TimedCharacterEffect) continue;
            RemoveEffect(activeEffects[i]);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            effect.OnTickEffect(character, dt);

            if (effect is not TimedCharacterEffect timdedEffect) continue;

            timdedEffect.remainingDuration -= dt;
            if (timdedEffect.remainingDuration <= 0f)
            {
                timdedEffect.OnEndEffect(character);
                activeEffects.RemoveAt(i);
                OnEffectEnded?.Invoke(timdedEffect);
            }
        }
    }

    public void ApplyEffect(CharacterEffect effect)
    {
        if (effect == null) return;

        CharacterEffect instance = Instantiate(effect);

        switch (instance)
        {
            case InstantCharacterEffect instantEffect:
                instantEffect.OnStartEffect(character);
                break;

            case TimedCharacterEffect timedEffect when timedEffect.stackable || !HasEffectByName(timedEffect.effectName):
                RegisterTimedEffect(timedEffect);
                break;

            case StaticCharacterEffect staticEffect when staticEffect.stackable || !HasEffectByName(staticEffect.effectName):
                RegisterStaticEffect(staticEffect);
                break;
        }
    }

    public void ApplyEffects(List<CharacterEffect> effects)
    {
        foreach (CharacterEffect effect in effects)
        {
            ApplyEffect(effect);
        }
    }

    public void RemoveEffect(CharacterEffect effect)
    {
        if (effect == null || !activeEffects.Contains(effect)) return;

        effect.OnEndEffect(character);
        activeEffects.Remove(effect);
        OnEffectEnded?.Invoke(effect);
    }


    public bool HasEffectByName(string effectName)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].effectName == effectName)
            {
                return true;
            }
        }
        return false;
    }

    private void RegisterTimedEffect(TimedCharacterEffect effect)
    {
        if (effect == null || activeEffects.Contains(effect)) return;
        
        effect.remainingDuration = effect.duration;
        effect.OnStartEffect(character);
        activeEffects.Add(effect);
        OnEffectStarted?.Invoke(effect);
    }

    private void RegisterStaticEffect(StaticCharacterEffect effect)
    {
        if (effect == null || activeEffects.Contains(effect)) return;

        effect.OnStartEffect(character);
        activeEffects.Add(effect);
        OnEffectStarted?.Invoke(effect);
    }
}
