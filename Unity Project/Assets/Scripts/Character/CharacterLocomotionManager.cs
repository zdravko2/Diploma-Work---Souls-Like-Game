using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterLocomotionManager : MonoBehaviour
{
    protected CharacterManager character;
    [SerializeField][Min(0)] float walkingSpeed = 2f;
    [SerializeField][Min(0)] float runningSpeedMultiplier = 2f;
    [SerializeField][Min(0)] float rotationSpeed = 15f;
    public bool gravityEnabled = true;
    public bool canMove = true;
    public bool canRotate = true;
    public bool canRun = true;
    public bool canJump = true;
    public bool isGrounded;
    public bool isRunning;
    
    protected virtual void Start()
    {
        character = GetComponent<CharacterManager>();
    }

    public virtual void HandleGroundedMovement(Vector3 movementDirection, float moveAmount)
    {
        float speed = (moveAmount > 0.5f) ? walkingSpeed * runningSpeedMultiplier :
                      (moveAmount > 0.1f) ? walkingSpeed : 0f;
        isRunning = moveAmount > 0.5f && canRun;

        Vector3 forward = character.controller.characterForward;
        float vertical = Vector3.Dot(movementDirection, forward);

        if (!character.animatorManager.applyRootMotion)
        {
            character.characterController.Move(movementDirection * speed * Time.deltaTime);
        }

        character.animatorManager.UpdateAnimatorMovementParameters(0, movementDirection.magnitude, moveAmount);
    }

    public virtual void HandleLockedOnMovement(Vector3 movementDirection, float moveAmount)
    {
        if (!character.combatManager.isLockedOn) return;
        isRunning = moveAmount > 0.5f && canRun;

        Vector3 desiredMovementDirection = movementDirection.normalized;

        float distanceToTarget = Vector3.Distance(character.transform.position, character.combatManager.lockedOnTarget.transform.position);
        float biasStrength = Mathf.Clamp(distanceToTarget * 0.02f, 0.01f, 0.03f);
        Vector3 biasedMovementDirection = (desiredMovementDirection + character.controller.lockOnTargetDirection * biasStrength).normalized;

        float speed = (moveAmount > 0.5f) ? walkingSpeed * runningSpeedMultiplier :
                      (moveAmount > 0.1f) ? walkingSpeed : 0f;

        if (!character.animatorManager.applyRootMotion && speed > 0f)
        {
            character.characterController.Move(biasedMovementDirection * speed * Time.deltaTime);
        }

        Vector3 forward = character.controller.characterForward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        float horizontal = Vector3.Dot(biasedMovementDirection, right);
        float vertical = Vector3.Dot(biasedMovementDirection, forward);

        character.animatorManager.UpdateAnimatorMovementParameters(character.controller.movementInput.x, character.controller.movementInput.y, moveAmount);
    }

    public virtual void HandleFaceDirection(Vector3 faceDirection)
    {
        if (faceDirection.sqrMagnitude < 0.001f)
        {
            faceDirection = transform.forward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(faceDirection);
        targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    public virtual void PeformDodge(Vector3 desiredDirection, float moveAmount)
    {
        const float inputDeadzone = 0.1f;

        Vector3 dodgeWorldDirection;

        if (moveAmount > inputDeadzone && desiredDirection.sqrMagnitude > 0.001f)
        {
            dodgeWorldDirection = desiredDirection.normalized;
        }
        else
        {
            dodgeWorldDirection = character.controller.characterForward;
        }

        dodgeWorldDirection.y = 0f;
        dodgeWorldDirection.Normalize();

        Vector3 dodgeLocalDirection = character.transform.InverseTransformDirection(dodgeWorldDirection);
        dodgeLocalDirection.y = 0f;
        dodgeLocalDirection.Normalize();

        float dodgeX = dodgeLocalDirection.x;
        float dodgeY = dodgeLocalDirection.z;

        character.animatorManager.UpdateAnimatorMovementParameters(dodgeX, dodgeY, 0f);
        character.animatorManager.PlayTargetActionAnimation(character.animatorManager.animations.Dodge, isDodging: true, crossFadeDuration: 0.1f);
    }

    float gravityForce = -10f;
    protected float verticalVelocity;

    public virtual void HandleGravity()
    {
        if (!gravityEnabled) return;
        
        if (character.characterController.isGrounded && verticalVelocity < 0f)
        {
            const float stickToGroundForce = -2f;
            verticalVelocity = stickToGroundForce;
        }
        else
        {
            const float terminalVelocity = -50f;
            verticalVelocity += gravityForce * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, terminalVelocity);
        }
        
        if (character.controller.enabled && character.characterController.enabled)
        {
            character.characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }
    }
}
