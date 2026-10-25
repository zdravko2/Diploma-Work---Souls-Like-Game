using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TutorialNPCActions
{
    [Header("Attacks")]
    public List<AIAttackDefinition> attacks = new List<AIAttackDefinition>();

    [Header("Defense")]
    public string blockStartAnimation = "Block Start";
    public string parryAnimation = "Parry Left";
    [Min(0.05f)] public float parryDuration = 0.45f;

    public AIAttackDefinition GetRandomAttack(float distance, float signedAngle, bool requireFit)
    {
        if (attacks == null || attacks.Count == 0) return null;

        List<AIAttackDefinition> valid = new List<AIAttackDefinition>();
        float totalWeight = 0f;

        for (int i = 0; i < attacks.Count; i++)
        {
            AIAttackDefinition attack = attacks[i];
            if (attack == null || string.IsNullOrWhiteSpace(attack.animationName)) continue;

            if (requireFit)
            {
                if (distance < attack.minDistance || distance > attack.maxDistance) continue;
                if (signedAngle < attack.minAngle || signedAngle > attack.maxAngle) continue;
            }

            valid.Add(attack);
            totalWeight += Mathf.Max(0.01f, attack.weight);
        }

        if (valid.Count == 0) return null;

        float pick = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < valid.Count; i++)
        {
            pick -= Mathf.Max(0.01f, valid[i].weight);
            if (pick <= 0f) return valid[i];
        }

        return valid[valid.Count - 1];
    }
}
