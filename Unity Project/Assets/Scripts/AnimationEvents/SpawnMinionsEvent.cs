using System;
using UnityEngine;

[Serializable]
[AnimatorStateEventMenu("Combat/Spawn Minions")]
public class SpawnMinionsEvent : AnimatorStateEvent
{
    private EnemyAIController controller;
    [SerializeField] GameObject minionPrefab;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (controller == null) controller = animator.GetComponent<EnemyAIController>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (controller == null || minionPrefab == null) return;

        Vector3 spawnPosition = controller.transform.position;
        spawnPosition.y = controller.transform.position.y;
        Quaternion spawnRotation = controller.transform.rotation;

        CharacterManager minion1 = GameObject.Instantiate(minionPrefab, spawnPosition + controller.transform.right * 2f, spawnRotation).GetComponent<CharacterManager>();
        CharacterManager minion2 = GameObject.Instantiate(minionPrefab, spawnPosition - controller.transform.right * 2f, spawnRotation).GetComponent<CharacterManager>();

        controller.minions.Add(minion1);
        controller.minions.Add(minion2);
    }
}
