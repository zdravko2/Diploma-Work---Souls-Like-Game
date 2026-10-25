using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    public static PlayerCamera Instance;
    [HideInInspector] public PlayerManager player;
    [HideInInspector] public Camera cameraObject;
    [SerializeField] Transform cameraPivot;

    [Header("Settings")]
    [SerializeField] private Vector3 defaultPosition = new Vector3(0, 1.5f, -3f);
    [SerializeField] private Vector2 rotationSpeed = new Vector2(1, 1);
    [SerializeField] private Vector2 pivotLimit = new Vector2(-60, 90);
    [SerializeField] [Min(0)] private float smoothTrackPlayer = 100;
    [SerializeField] [Min(0)] private float smoothLockOn = 7;
    [SerializeField] private Vector2 lockOnOffset = new Vector2(0, -3);
    [Header("Collision")]
    [SerializeField][Min(0)] private float collisionRadius = 0.2f;

    [Header("Controls")]
    public bool controlsEnabled = true;
    public bool followPlayer = true;
    public bool avoidCollision = true;

    [Header("Camera Values")]
    Vector3 cameraVelocity;
    Vector2 lookAngle;


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            cameraObject = GetComponentInChildren<Camera>();
        }
        else
        {
            Destroy(gameObject);
        }
        
        SetCursorState(CursorLockMode.Locked);
    }

    void LateUpdate()
    {
        if (player == null) return;

        cameraPivot.localPosition = new Vector3(defaultPosition.x, defaultPosition.y, 0);
        cameraObject.transform.localPosition = new Vector3(0, 0, defaultPosition.z);

        if (followPlayer) FollowTarget(player.transform);
        if (avoidCollision) AvoidCollision();
        
        if (player.combatManager.isLockedOn)
        {
            Transform target = player.combatManager.lockedOnTarget.GetComponentInChildren<LockOnTransform>()?.transform;
            if (target != null)
            {
                LookAtTarget(target);
            }
            else
            {
                LookAtTarget(player.combatManager.lockedOnTarget.transform);
            }
        }
        else
        {
            if (controlsEnabled) LookRotation();
        }
    }

    public void SetCursorState(CursorLockMode lockMode)
    {
        Cursor.lockState = lockMode;
    }

    public void SetPosition(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }

    private void FollowTarget(Transform target)
    {
        Vector3 targetPos = Vector3.SmoothDamp(transform.position, target.position, ref cameraVelocity, 1 / smoothTrackPlayer);
        transform.position = targetPos;
    }

    private void LookRotation()
    {
        Vector2 camInput = PlayerInputManager.Instance.LookInput;

        float currentPitch = cameraPivot.localEulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f;

        lookAngle.x = transform.eulerAngles.y;
        lookAngle.y = currentPitch;

        lookAngle.x += camInput.x * rotationSpeed.x * SettingsManager.Instance.CurrentSettings.mouseSensitivityX;
        lookAngle.y -= camInput.y * rotationSpeed.y * SettingsManager.Instance.CurrentSettings.mouseSensitivityY;

        lookAngle.y = Mathf.Clamp(lookAngle.y, pivotLimit.x, pivotLimit.y);
        lookAngle.y = Mathf.Clamp(lookAngle.y, -89.9f, 89.9f);

        transform.rotation = Quaternion.Euler(0f, lookAngle.x, 0f);
        cameraPivot.localRotation = Quaternion.Euler(lookAngle.y, 0f, 0f);
    }

    private void LookAtTarget(Transform target)
    {
        if (target == null) return;

        Vector3 offset =
            transform.right * lockOnOffset.x +
            transform.up * lockOnOffset.y;

        Vector3 worldOffsetPos = target.position + offset;
        Vector3 dir = worldOffsetPos - cameraPivot.position;

        float flatDist = new Vector2(dir.x, dir.z).magnitude;
        flatDist = Mathf.Max(flatDist, 0.25f);

        float desiredYaw = lookAngle.x;
        if (flatDist > 0.001f)
        {
            desiredYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        float desiredPitch = -Mathf.Atan2(dir.y, flatDist) * Mathf.Rad2Deg;
        desiredPitch = Mathf.Clamp(desiredPitch, pivotLimit.x, pivotLimit.y);

        float deltaYaw = Mathf.DeltaAngle(lookAngle.x, desiredYaw);
        float deltaPitch = Mathf.DeltaAngle(lookAngle.y, desiredPitch);

        lookAngle.x += deltaYaw * smoothLockOn * Time.deltaTime;
        lookAngle.y += deltaPitch * smoothLockOn * Time.deltaTime;
        
        lookAngle.y = Mathf.Clamp(lookAngle.y, pivotLimit.x, pivotLimit.y);
        lookAngle.y = Mathf.Clamp(lookAngle.y, -89.9f, 89.9f);

        transform.rotation = Quaternion.Euler(0f, lookAngle.x, 0f);
        cameraPivot.localRotation = Quaternion.Euler(lookAngle.y, 0f, 0f);
    }

    private void AvoidCollision()
    {
        Vector3 targetPos = defaultPosition;
        RaycastHit hit;
        Vector3 direction = cameraObject.transform.position - cameraPivot.position;
        direction.Normalize();

        if (Physics.SphereCast(cameraPivot.position, collisionRadius, direction, out hit, MathF.Abs(targetPos.z), GameManager.Instance.environmentLayer))
        {
            float distance = Vector3.Distance(cameraPivot.position, hit.point);
            targetPos.z = -(distance - collisionRadius);
        }

        if (Mathf.Abs(targetPos.z) < collisionRadius)
        {
            targetPos.z = -collisionRadius;
        }

        Vector3 finalPos = new Vector3(0, 0, Mathf.Lerp(cameraObject.transform.position.z, targetPos.z, 1));
        cameraObject.transform.localPosition = finalPos;
    }
}
