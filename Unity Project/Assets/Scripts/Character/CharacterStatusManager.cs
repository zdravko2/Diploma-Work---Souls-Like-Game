using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterStatusManager : MonoBehaviour
{
    [Min(0)] public int vitality = 10, endurance = 10, mind = 10, strength = 10, dexterity = 10;
    public bool isDead { get; protected set; }
    public float currentHealth { get; protected set; }
    public float maxHealth { get; protected set; }
    public float currentStamina { get; protected set; }
    public float maxStamina { get; protected set; }
    public float currentPoise { get; protected set; }
    public float maxPoise { get; protected set; }
    public event Action OnCharacterDeath;
    public event Action OnPoiseDamaged;
    public event Action OnPoiseBroken;
    public event Action<DamageData> OnDamageInflicted;
    public event Action<float> OnHealthChanged;
    public event Action<float> OnMaxHealthChanged;
    public event Action<float> OnStaminaChanged;
    public event Action<float> OnMaxStaminaChanged;
    public event Action<float> OnPoiseChanged;
    public event Action<float> OnMaxPoiseChanged;

    protected virtual void Awake()
    {
        InitializeStats(
            vitality,
            endurance,
            mind,
            strength,
            dexterity
            );
    }

    public virtual void InitializeStats(int vitality, int endurance, int mind, int strength, int dexterity)
    {
        maxHealth = CalculateTotalHealth(vitality);
        currentHealth = maxHealth;
        OnMaxHealthChanged?.Invoke(maxHealth);
        OnHealthChanged?.Invoke(currentHealth);

        maxStamina = CalculateTotalStamina(endurance);
        currentStamina = maxStamina;
        OnMaxStaminaChanged?.Invoke(maxStamina);
        OnStaminaChanged?.Invoke(currentStamina);

        maxPoise = CalculateTotalPoise(strength);
        currentPoise = maxPoise;
        OnMaxPoiseChanged?.Invoke(maxPoise);
        OnPoiseChanged?.Invoke(currentPoise);
    }

    public virtual void LoseHealth(float amount)
    {
        if (isDead) return;
        if (amount <= 0) return;
        currentHealth = Mathf.Max(currentHealth - amount, 0);
        OnHealthChanged?.Invoke(currentHealth);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    public virtual void RegainHealth(float amount)
    {
        if (isDead) return;
        if (amount <= 0) return;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        OnHealthChanged?.Invoke(currentHealth);
    }

    public virtual void LoseStamina(float amount)
    {
        if (isDead) return;
        if (amount <= 0) return;
        currentStamina = Mathf.Max(currentStamina - amount, 0);
        OnStaminaChanged?.Invoke(currentStamina);
    }

    public virtual void RegainStamina(float amount)
    {
        if (isDead) return;
        if (amount <= 0) return;
        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
        OnStaminaChanged?.Invoke(currentStamina);
    }

    public virtual void LosePoise(float amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentPoise = Mathf.Max(currentPoise - amount, 0);
        OnPoiseChanged?.Invoke(currentPoise);

        if (amount > 0) OnPoiseDamaged?.Invoke();

        if (currentPoise <= 0)
        {
            currentPoise = 0;
            OnPoiseBroken?.Invoke();
            RegainPoise(maxPoise);
        }
    }

    public virtual void RegainPoise(float amount)
    {
        currentPoise = Mathf.Min(currentPoise + amount, maxPoise);
        OnPoiseChanged?.Invoke(currentPoise);
    }

    protected virtual float CalculateTotalHealth(int health)
    {
        float totalHealth = health * 1;
        return Mathf.RoundToInt(totalHealth);
    }

    protected virtual float CalculateTotalStamina(int endurance)
    {
        float totalStamina = endurance * 1;
        return Mathf.RoundToInt(totalStamina);
    }
    
    protected virtual float CalculateTotalPoise(int endurance)
    {
        float totalPoise = endurance * 1;
        return Mathf.RoundToInt(totalPoise);
    }

    public virtual void InflictDamage(DamageData damageData)
    {
        float physicalResistance = 0;
        float magicalResistance = 0;
        float totalDamage = damageData.physical - physicalResistance * damageData.physical +
                            damageData.magical - magicalResistance * damageData.magical;

        LoseHealth(totalDamage);
        LoseStamina(damageData.stamina);
        LosePoise(damageData.poise);

        OnDamageInflicted?.Invoke(damageData);
    }

    void Die()
    {
        isDead = true;
        OnCharacterDeath?.Invoke();
    }
}
