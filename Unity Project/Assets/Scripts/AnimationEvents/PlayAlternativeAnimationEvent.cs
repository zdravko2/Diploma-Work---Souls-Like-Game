using System;
using UnityEngine;

[Serializable]
[AnimatorStateEventColor("#00ff88")]
[AnimatorStateEventMenu("Animation/Play Alternative Animation")]
public class PlayAlternativeAnimationEvent : AnimatorStateEvent
{
    private CharacterManager character;

    [SerializeField] string alternativeAnimation = "";
    [SerializeField] bool moveToCurrentRootMotionPosition = true;
    [SerializeField] bool preserveCurrentY = true;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        if (character == null) character = animator.GetComponent<CharacterManager>();
    }

    public override void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager)
    {
        if (character == null)
            return;

        if (moveToCurrentRootMotionPosition)
        {
            Vector3 targetPosition = animator.rootPosition;

            if (preserveCurrentY)
                targetPosition.y = character.transform.position.y;

            MoveCharacterTo(character, targetPosition);
        }

        character.animatorManager.PlayTargetAnimation(
            alternativeAnimation,
            animatorLayer: -1,
            normalizedTimeOffset: seconds
        );
    }

    private void MoveCharacterTo(CharacterManager character, Vector3 targetPosition)
    {
        Rigidbody rb = character.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (rb.isKinematic)
                rb.MovePosition(targetPosition);
            else
                rb.position = targetPosition;

            return;
        }

        CharacterController cc = character.GetComponent<CharacterController>();
        if (cc != null)
        {
            bool wasEnabled = cc.enabled;
            cc.enabled = false;
            character.transform.position = targetPosition;
            cc.enabled = wasEnabled;
            return;
        }

        character.transform.position = targetPosition;
    }
}