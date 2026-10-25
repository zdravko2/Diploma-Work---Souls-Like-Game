using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class WeaponItemAction : ScriptableObject
{
    public int id;
    public float staminaCost;

    public virtual void PerformAction(CharacterManager character, WeaponManager weapon)
    {
        
    }
}
