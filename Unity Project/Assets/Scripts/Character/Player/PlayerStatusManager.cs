using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PlayerStatusManager : CharacterStatusManager
{
    public float currentMana { get; private set; }
    public float maxMana { get; private set; }

    public delegate void ManaChanged(float value);
    public delegate void MaxManaChanged(float value);
    public event ManaChanged OnManaChanged;
    public event MaxManaChanged OnMaxManaChanged;

    public override void InitializeStats(int vigor, int endurance, int mind, int strength, int dexterity)
    {
        base.InitializeStats(vigor, endurance, mind, strength, dexterity);

        maxMana = CalculateTotalMana(mind);
        currentMana = maxMana;
        OnMaxManaChanged?.Invoke(maxMana);
        OnManaChanged?.Invoke(currentMana);
    }

    public virtual void LoseMana(float amount)
    {
        if (amount <= 0) return;
        currentMana = Mathf.Max(currentMana - amount, 0);
        OnManaChanged?.Invoke(currentMana);
    }

    public virtual void RegainMana(float amount)
    {
        if (isDead) return;
        if (amount <= 0) return;
        currentMana = Mathf.Min(currentMana + amount, maxMana);
        OnManaChanged?.Invoke(currentMana);
    }

    protected virtual float CalculateTotalMana(int mind)
    {
        float totalMana = 0;

        totalMana = mind * 1;

        return Mathf.RoundToInt(totalMana);
    }
}
