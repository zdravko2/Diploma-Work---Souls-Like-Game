using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialNPCMoveId
{
    None,
    BasicAttack,
    BlockLessonAttack,
    ParryLessonAttack,
    FreeFightAttack
}

[Serializable]
public class TutorialNPCMoveSet
{
    [Header("Held Defense")]
    public string heldBlockAnimationName = "Block Idle";
    public string parryAnimationName = "Parry Left";

    [Header("Teaching Attacks")]
    public AIAttackDefinition basicAttack;
    public AIAttackDefinition blockLessonAttack;
    public AIAttackDefinition parryLessonAttack;

    [Header("Final Fight")]
    public List<AIAttackDefinition> freeFightAttacks = new List<AIAttackDefinition>();

    public AIAttackDefinition GetAttack(TutorialNPCMoveId id)
    {
        switch (id)
        {
            case TutorialNPCMoveId.BasicAttack: return basicAttack;
            case TutorialNPCMoveId.BlockLessonAttack: return blockLessonAttack;
            case TutorialNPCMoveId.ParryLessonAttack: return parryLessonAttack;
            case TutorialNPCMoveId.FreeFightAttack:
                if (freeFightAttacks == null || freeFightAttacks.Count == 0) return null;
                return freeFightAttacks[UnityEngine.Random.Range(0, freeFightAttacks.Count)];
            default: return null;
        }
    }
}
