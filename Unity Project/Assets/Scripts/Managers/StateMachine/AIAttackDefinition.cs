using System;
using UnityEngine;

[Serializable]
public class AIAttackDefinition
{
    public string animationName;
    public int requiredPhase;

    [Min(0f)] public float minDistance = 1f;
    [Min(0f)] public float maxDistance = 3f;
    [Min(0f)] public float idealDistance = 2f;

    [Range(-180f, 180f)] public float minAngle = -45f;
    [Range(-180f, 180f)] public float maxAngle = 45f;

    [Min(0f)] public float weight = 1f;
    [Min(0f)] public float cooldown = 2f;
    [Min(0f)] public float positionTimeout = 2f;

    public bool applyRootMotion = true;
    public bool canBeInterrupted = true;
    public bool canMove = false;
    public bool canRotate = false;
    public bool hasHyperArmor = true;
    public int comboOffset = 1;
}