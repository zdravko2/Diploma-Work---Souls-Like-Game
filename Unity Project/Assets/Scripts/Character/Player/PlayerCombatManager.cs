using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatManager : CharacterCombatManager
{
    public override CharacterManager FindTarget()
    {
        if (character == null) return null;
        
        Collider[] targetColliders = Physics.OverlapSphere(
            character.transform.position,
            lockOnRadius,
            GameManager.Instance.characterLayer
        );

        float closestToScreenCenter = 180f;
        CharacterManager closestTarget = null;
        Transform camera = PlayerCamera.Instance.cameraObject.transform;

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Vector3 offset = new Vector3(0, 2, 0);
            CharacterManager target = targetColliders[i].GetComponent<CharacterManager>();
            if (target == null || target.statusManager.isDead) continue;
            if (target == character) continue;
            if (target.combatManager.canBeLockedOn == false) continue;
            if (target.transform.root == character.transform.root) continue;
            if (target.characterGroup == character.characterGroup) continue;

            Vector3 direction = (target.transform.position + offset - camera.position).normalized;
            float distance = Vector3.Distance(target.transform.position + offset, character.transform.position + offset);
            float angle = Vector3.Angle(camera.forward, direction);

            if (angle > lockOnFOV / 2 && distance > lockOnRadius) continue;

            RaycastHit hit;
            if (Physics.Linecast(
                target.transform.position + offset,
                camera.position,
                out hit,
                GameManager.Instance.environmentLayer)) continue;

            if (angle < closestToScreenCenter)
            {
                closestToScreenCenter = angle;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    public virtual CharacterManager FindTargetRelativeToLockOn(Camera cam, float horizontalDirection)
    {
        if (character == null) return null;
        if (lockedOnTarget == null) return FindTarget();

        horizontalDirection = Mathf.Sign(horizontalDirection);
        if (horizontalDirection == 0f) return null;

        Vector3 currentDir = lockedOnTarget.transform.position - character.transform.position;
        currentDir.y = 0f;
        if (currentDir.sqrMagnitude <= 0.0001f) return null;
        currentDir.Normalize();

        Collider[] targetColliders = Physics.OverlapSphere(
            character.transform.position,
            lockOnRadius,
            GameManager.Instance.characterLayer
        );

        CharacterManager bestTarget = null;
        float bestAngle = float.MaxValue;

        for (int i = 0; i < targetColliders.Length; i++)
        {
            Vector3 offset = new Vector3(0, 1, 0);
            CharacterManager target = targetColliders[i].GetComponent<CharacterManager>();
            if (target == null || target == lockedOnTarget) continue;
            if (target.statusManager.isDead) continue;
            if (target == character) continue;
            if (target.combatManager.canBeLockedOn == false) continue;
            if (target.transform.root == character.transform.root) continue;
            if (target.characterGroup == character.characterGroup) continue;

            Vector3 candidateDir = (target.transform.position + offset) - (character.transform.position + offset);
            candidateDir.y = 0f;

            float distance = candidateDir.magnitude;
            if (distance > lockOnRadius || distance <= 0.0001f) continue;

            candidateDir /= distance;

            RaycastHit hit;
            if (Physics.Linecast(
                target.transform.position + offset,
                character.transform.position + offset,
                out hit,
                GameManager.Instance.environmentLayer)) continue;

            float signedAngle = Vector3.SignedAngle(currentDir, candidateDir, Vector3.up);

            if (horizontalDirection > 0f && signedAngle <= 0f) continue;
            if (horizontalDirection < 0f && signedAngle >= 0f) continue;

            float absAngle = Mathf.Abs(signedAngle);

            if (absAngle < bestAngle)
            {
                bestAngle = absAngle;
                bestTarget = target;
            }
        }

        return bestTarget;
    }
}
