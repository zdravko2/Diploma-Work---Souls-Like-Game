using System;
using UnityEngine;

[Serializable]
public class TutorialNPCBrainSettings
{
    [Header("Dialogue")]
    [Min(0f)] public float delayAfterDialogue = 0.75f;
    public string[] openingDialogue =
    {
        "Easy. I am not here to kill you.",
        "I will show you the basics. Read each lesson, then act."
    };
    public string[] completionDialogue =
    {
        "Enough. You understand the basics.",
        "Past this room, I will not save you."
    };

    [Header("Lesson Protection")]
    public bool protectNPCBeforeFinalFight = true;
    public bool protectPlayerBeforeFinalFight = true;

    [Header("Spacing")]
    [Min(0f)] public float preferredMinDistance = 1.4f;
    [Min(0f)] public float preferredMaxDistance = 2.8f;
    [Min(0f)] public float distanceTolerance = 0.35f;
    [Min(0f)] public float chaseSprintDistance = 5f;
    [Min(0f)] public float retreatDistance = 2.5f;

    [Header("Combat Decisions")]
    [Range(0f, 1f)] public float aggression = 0.65f;
    [Range(0f, 1f)] public float pressure = 0.5f;
    [Range(0f, 1f)] public float caution = 0.25f;
    [Range(0f, 1f)] public float mobility = 0.45f;
    [Range(0f, 1f)] public float unpredictability = 0.1f;
    [Min(0f)] public float postAttackDecisionDelay = 0.45f;
    [Min(0f)] public float repeatAttackDelay = 2.25f;
    [Min(0f)] public float strafeDurationMin = 0.7f;
    [Min(0f)] public float strafeDurationMax = 1.4f;
    [Min(0f)] public float finalFightStopHealthPercent = 0.5f;

    [Header("Navigation")]
    [Min(0f)] public float navSampleRadius = 2f;
    [Min(0f)] public float navLinkCompleteDistance = 0.25f;
}
