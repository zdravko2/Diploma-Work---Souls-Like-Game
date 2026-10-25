using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterSlot
{
    CharacterSlot
}

public enum EffectTriggerType
{
    Instant,
    Static,
    Timed,
}

public enum CharacterGroup
{
    Neutral,
    Player,
    Enemy
}

public enum DamageType
{
    Special,
    Effect,
    Piercing,
    Slashing,
    Bludgeoning
}

public interface IDamageable
{
    void TakeDamage(CharacterEffect effect, Vector3 hitDirection, Vector3 hitPoint);
}