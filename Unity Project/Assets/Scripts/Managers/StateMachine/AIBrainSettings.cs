using UnityEngine;
using System;

[Serializable]
public class AIBrainSettings
{
    [Header("Temperament")]
    [Range(0f, 1f)] public float aggression = 0.7f;
    [Range(0f, 1f)] public float unpredictability = 0.3f;
    [Range(0f, 1f)] public float pressure = 0.8f;
    [Range(0f, 1f)] public float mobility = 0.5f;
    [Range(0f, 1f)] public float caution = 0.2f;

    [Header("Spacing")]
    [Min(0f)] public float preferredMinDistance = 1f;
    [Min(0f)] public float preferredMaxDistance = 3f;
    [Min(0f)] public float chaseSprintDistance = 4f;
    [Min(0f)] public float retreatDistance = 3f;
    [Min(0f)] public float distanceTolerance = 0.5f;

    [Header("Patrol")]
    public bool awaitEnemyBeforeActing = false;
    [Min(0f)] public float patrolRadius = 5f;
    [Min(0f)] public float patrolPointTolerance = 0.5f;
    [Min(0f)] public float patrolTimeout = 10f;
    [Min(0f)] public float idleDurationMin = 3f;
    [Min(0f)] public float idleDurationMax = 5f;

    [Header("Combat")]
    [Min(0f)] public float strafeDurationMin = 1f;
    [Min(0f)] public float strafeDurationMax = 2f;
    [Min(0f)] public float postAttackDecisionDelay = 0.15f;
    [Min(0f)] public float hyperArmorCooldown = 2f;

    [Header("Runtime")]
    [Min(0f)] public float targetRefreshInterval = 1f;
    public bool keepTargetLockOn = true;
}

