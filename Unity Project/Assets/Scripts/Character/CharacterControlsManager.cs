using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterControlsManager : MonoBehaviour
{
    public CharacterManager character { get; protected set; }
    public Vector3 desiredMovementDirection { get; protected set; }
    public Vector3 lockOnTargetDirection { get; protected set; }
    public Vector3 characterForward { get; protected set; }
    public Vector2 movementInput { get; protected set; }
    public bool sprintInput { get; protected set; }
    public float moveAmount { get; protected set; }
    public bool primaryActionInput { get; protected set; }
    public bool secondaryActionInput { get; protected set; }

    protected virtual void Start()
    {
        character = GetComponent<CharacterManager>();
    }

    protected virtual void Update()
    {
        UpdateMovementParameters();
    }

    protected virtual void UpdateMovementParameters()
    {
        characterForward = character.transform.forward;
        float inputAmount = Mathf.Clamp01(movementInput.magnitude);
        moveAmount = Mathf.Clamp01(Mathf.Abs(movementInput.x) + Mathf.Abs(movementInput.y));
        moveAmount = moveAmount > 0.1f ? (sprintInput && character.locomotionManager.canRun ? 1f : 0.5f) : 0f;

        if (character.combatManager.lockedOnTarget != null)
        {
            lockOnTargetDirection = character.combatManager.lockedOnTarget.transform.position - character.transform.position;
            lockOnTargetDirection = new Vector3(lockOnTargetDirection.x, 0, lockOnTargetDirection.z);
            lockOnTargetDirection.Normalize();
        }
        else
        {
            lockOnTargetDirection = desiredMovementDirection;
        }
    }
}
