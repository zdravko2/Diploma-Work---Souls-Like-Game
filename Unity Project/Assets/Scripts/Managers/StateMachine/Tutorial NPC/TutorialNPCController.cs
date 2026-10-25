using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TutorialNPCController : CharacterControlsManager
{
    [Header("Tutorial AI")]
    public string statePreview;
    public TutorialNPCBrainSettings brain = new TutorialNPCBrainSettings();
    public TutorialNPCActions actions = new TutorialNPCActions();
    [SerializeField] List<TutorialNPCLesson> lessons = new List<TutorialNPCLesson>();

    [Header("Navigation")]
    [SerializeField] bool useNavMesh = true;
    [SerializeField] NavMeshAgent navAgent;

    TutorialNPCStateMachine stateMachine;
    PlayerManager tutorialPlayer;
    CharacterManager tutorialTarget;
    GameObject dialogueObject;
    Vector3 homePosition;
    bool navActive;
    bool navSprint;
    string currentState;
    string previousState;

    string[] activeDialogueLines;
    int activeDialogueIndex;
    bool dialogueOpen;
    bool dialogueComplete = true;

    public TutorialNPCBrainSettings Brain => brain;
    public TutorialNPCActions Actions => actions;
    public List<TutorialNPCLesson> Lessons => lessons;
    public PlayerManager TutorialPlayer => tutorialPlayer;
    public CharacterManager TutorialTarget => tutorialTarget;
    public Vector3 HomePosition => homePosition;
    public bool HasTutorialTarget => tutorialTarget != null && !tutorialTarget.statusManager.isDead;
    public bool IsTutorialRunning => stateMachine != null && stateMachine.IsTutorialRunning;
    public bool DialogueComplete => dialogueComplete;
    public bool IsWaitingForDialogueInput => dialogueOpen;
    public bool CanIssueAction => !character.statusManager.isDead && !character.animatorManager.isInAnimation;
    public float DistanceToTarget => HasTutorialTarget ? Vector3.Distance(transform.position, tutorialTarget.transform.position) : Mathf.Infinity;

    public Vector3 DirectionToTarget
    {
        get
        {
            if (!HasTutorialTarget) return Vector3.zero;
            Vector3 direction = tutorialTarget.transform.position - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }

    public float SignedAngleToTarget => HasTutorialTarget ? Vector3.SignedAngle(transform.forward, DirectionToTarget, Vector3.up) : 0f;

    protected override void Start()
    {
        base.Start();
        homePosition = transform.position;
        SetupHitboxes();
        SetupNavMesh();
        stateMachine = new TutorialNPCStateMachine(this);
    }

    protected override void Update()
    {
        if (character.statusManager.isDead) return;

        if (character.animatorManager.isPerformingAction) character.animatorManager.UpdateAnimatorMovementParameters(0, 0, 0);

        stateMachine?.Tick();
        UpdateNavMeshMovementIntent();
        UpdateMovementParameters();
        UpdateStatePreview();
        character.locomotionManager.HandleGravity();

        if (character.locomotionManager.canMove)
        {
            if (character.combatManager.isLockedOn) character.locomotionManager.HandleLockedOnMovement(desiredMovementDirection, moveAmount);
            else character.locomotionManager.HandleGroundedMovement(desiredMovementDirection, moveAmount);
        }

        if (character.locomotionManager.canRotate)
        {
            if (character.combatManager.isLockedOn) character.locomotionManager.HandleFaceDirection(lockOnTargetDirection);
            else character.locomotionManager.HandleFaceDirection(desiredMovementDirection);
        }
    }

    void LateUpdate()
    {
        SyncNavAgent();
    }

    public void BeginTutorial(PlayerManager player, GameObject focusObject)
    {
        if (player == null || stateMachine == null || IsTutorialRunning) return;

        CharacterManager target = player.GetComponent<CharacterManager>();
        if (target == null || target.statusManager.isDead) return;

        tutorialPlayer = player;
        tutorialTarget = target;
        dialogueObject = focusObject != null ? focusObject : gameObject;
        character.combatManager.SetLockOnTarget(target);
        stateMachine.RequestTutorialStart();
    }

    public void EndTutorial()
    {
        ClearPlayerDialogueObject();
        tutorialPlayer = null;
        tutorialTarget = null;
        character.combatManager.SetLockOnTarget(null);
        StopNavigation();
        CloseDialogue();
        EndHeldBlock();
    }

    public void ShowDialogueLines(string[] lines)
    {
        if (lines == null || lines.Length == 0)
        {
            dialogueComplete = true;
            return;
        }

        activeDialogueLines = lines;
        activeDialogueIndex = 0;
        dialogueOpen = true;
        dialogueComplete = false;
        SetPlayerDialogueObject();
        ShowDialogue(activeDialogueLines[activeDialogueIndex]);
    }

    public void AdvanceDialogue(PlayerManager player)
    {
        if (!dialogueOpen) return;
        if (tutorialPlayer != null && player != tutorialPlayer) return;

        activeDialogueIndex++;
        if (activeDialogueIndex >= activeDialogueLines.Length)
        {
            CloseDialogue();
            return;
        }

        ShowDialogue(activeDialogueLines[activeDialogueIndex]);
    }

    public void ShowDialogue(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (PlayerUIManager.Instance == null || PlayerUIManager.Instance.popUpManager == null) return;
        PlayerUIManager.Instance.popUpManager.ShowDialogueMessage(message);
    }

    public void CloseDialogue()
    {
        dialogueOpen = false;
        dialogueComplete = true;
        activeDialogueLines = null;
        activeDialogueIndex = 0;
        ClearPlayerDialogueObject();

        if (PlayerUIManager.Instance == null || PlayerUIManager.Instance.popUpManager == null) return;
        PlayerUIManager.Instance.popUpManager.CloseDialogueMessage();
    }

    void SetPlayerDialogueObject()
    {
        if (tutorialPlayer == null) return;
        if (tutorialPlayer.controller is PlayerController playerController) playerController.dialogueObject = dialogueObject;
    }

    void ClearPlayerDialogueObject()
    {
        if (tutorialPlayer == null) return;
        if (tutorialPlayer.controller is PlayerController playerController && playerController.dialogueObject == dialogueObject) playerController.dialogueObject = null;
    }

    public void FaceTarget()
    {
        if (!HasTutorialTarget) return;
        character.combatManager.SetLockOnTarget(tutorialTarget);
    }

    public void MoveToCombatRange(float desiredDistance)
    {
        if (!HasTutorialTarget)
        {
            StopNavigation();
            return;
        }

        Vector3 destination = tutorialTarget.transform.position - DirectionToTarget * desiredDistance;
        bool sprint = DistanceToTarget > brain.chaseSprintDistance;
        SetNavDestination(destination, sprint, brain.distanceTolerance);
    }

    public void MoveToAttackRange(AIAttackDefinition attack)
    {
        if (attack == null)
        {
            MoveToCombatRange(brain.preferredMinDistance);
            return;
        }

        MoveToCombatRange(attack.idealDistance);
    }

    public void RetreatFromTarget()
    {
        if (!HasTutorialTarget)
        {
            StopNavigation();
            return;
        }

        Vector3 destination = character.transform.position - DirectionToTarget * brain.retreatDistance;
        SetNavDestination(destination, brain.mobility > 0.35f || brain.caution > 0.5f, brain.distanceTolerance);
    }

    public void StrafeTarget(bool right)
    {
        if (!HasTutorialTarget)
        {
            StopNavigation();
            return;
        }

        Vector3 tangent = Vector3.Cross(Vector3.up, DirectionToTarget) * (right ? 1f : -1f);
        Vector3 center = tutorialTarget.transform.position - DirectionToTarget * brain.preferredMinDistance;
        Vector3 destination = center + tangent * Mathf.Lerp(0.75f, 1.5f, brain.mobility);
        SetNavDestination(destination, false, brain.distanceTolerance);
    }

    public bool CanSeeTarget()
    {
        if (!HasTutorialTarget) return false;

        Vector3 start = transform.position + Vector3.up * 1.5f;
        Vector3 end = tutorialTarget.transform.position + Vector3.up * 1.5f;
        Vector3 direction = end - start;

        return !Physics.Raycast(start, direction.normalized, direction.magnitude, GameManager.Instance.environmentLayer);
    }

    public bool AttackFits(AIAttackDefinition attack)
    {
        if (attack == null) return false;
        return DistanceToTarget >= attack.minDistance - brain.distanceTolerance &&
               DistanceToTarget <= attack.maxDistance + brain.distanceTolerance &&
               SignedAngleToTarget >= attack.minAngle &&
               SignedAngleToTarget <= attack.maxAngle;
    }

    public bool TryPlayAttack(AIAttackDefinition attack)
    {
        if (attack == null || !CanIssueAction || !character.combatManager.canAttack || string.IsNullOrWhiteSpace(attack.animationName)) return false;

        StopNavigation();
        character.animatorManager.PlayTargetAttackAnimation(
            targetAnimation: attack.animationName,
            applyRootMotion: attack.applyRootMotion,
            canBeInterrupted: attack.canBeInterrupted,
            canMove: attack.canMove,
            canRotate: attack.canRotate,
            hasHyperArmor: attack.hasHyperArmor,
            comboOffset: attack.comboOffset);
        return true;
    }

    public void StartHeldBlock()
    {
        StopNavigation();
        character.combatManager.isBlocking = true;
        character.animatorManager.UpdateAnimatorBlockParameters(true);

        if (!string.IsNullOrWhiteSpace(actions.blockStartAnimation) && CanIssueAction)
        {
            character.animatorManager.PlayTargetBlockAnimation(actions.blockStartAnimation, canBeInterrupted: true);
        }
    }

    public void EndHeldBlock()
    {
        character.combatManager.isBlocking = false;
        character.animatorManager.UpdateAnimatorBlockParameters(false);
    }

    public bool TryParry()
    {
        if (!CanIssueAction || string.IsNullOrWhiteSpace(actions.parryAnimation)) return false;
        StopNavigation();
        character.animatorManager.PlayTargetBlockAnimation(actions.parryAnimation, canBeInterrupted: true);
        character.combatManager.isParrying = true;
        return true;
    }

    public void EndParry()
    {
        character.combatManager.isParrying = false;
    }

    public bool FinishedAction()
    {
        return !character.animatorManager.isPerformingAction &&
               !character.animatorManager.isInAnimation &&
               !character.combatManager.isAttacking &&
               !character.combatManager.isHitReacting &&
               !character.combatManager.isStunned;
    }

    public void SetMovementIntent(Vector3 worldDirection, bool sprint)
    {
        navActive = false;
        ApplyMovementIntent(worldDirection, sprint);
    }

    public void StopMoving()
    {
        desiredMovementDirection = Vector3.zero;
        movementInput = Vector2.zero;
        sprintInput = false;
    }

    void SetupHitboxes()
    {
        if (character.inventoryManager.GetByType<WeaponItem>() is WeaponItem weaponItem)
        {
            character.equipmentManager.AssignWeaponToMainHand(weaponItem);
        }

        character.equipmentManager.EquipMainHandWeapon();
    }

    void SetupNavMesh()
    {
        Vector3 initialPosition = gameObject.transform.position;
        if (!useNavMesh) return;

        if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null) navAgent = gameObject.AddComponent<NavMeshAgent>();

        navAgent.updatePosition = false;
        navAgent.updateRotation = false;
        navAgent.autoTraverseOffMeshLink = false;
        navAgent.autoBraking = true;
        navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        StartCoroutine(AwaitNavMesh(initialPosition));
    }

    IEnumerator AwaitNavMesh(Vector3 targetPosition)
    {
        navAgent.enabled = false;
        NavMeshHit hit;
        while (!NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas))
        {
            if (Vector3.Distance(targetPosition, hit.position) > 1f)
                yield return null;
            else
                break;
        }

        navAgent.Warp(targetPosition);
        navAgent.nextPosition = transform.position;

        navAgent.enabled = true;
    }

    void SyncNavAgent()
    {
        if (!useNavMesh || navAgent == null || !navAgent.isOnNavMesh) return;
        navAgent.nextPosition = transform.position;
    }

    void UpdateNavMeshMovementIntent()
    {
        if (!navActive || navAgent == null || !navAgent.isOnNavMesh) return;

        SyncNavAgent();

        if (navAgent.isOnOffMeshLink)
        {
            MoveAcrossNavLink();
            return;
        }

        Vector3 velocity = navAgent.desiredVelocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude <= 0.001f)
        {
            ApplyMovementIntent(Vector3.zero, false);
            return;
        }

        ApplyMovementIntent(velocity.normalized, navSprint);
    }

    void MoveAcrossNavLink()
    {
        Vector3 end = navAgent.currentOffMeshLinkData.endPos;
        Vector3 direction = end - transform.position;
        direction.y = 0f;

        if (direction.magnitude <= brain.navLinkCompleteDistance)
        {
            navAgent.CompleteOffMeshLink();
            navAgent.Warp(transform.position);
            SyncNavAgent();
            return;
        }

        ApplyMovementIntent(direction.normalized, navSprint);
    }

    public bool SetNavDestination(Vector3 destination, bool sprint, float stoppingDistance = 0.1f)
    {
        if (!useNavMesh || navAgent == null || !navAgent.isOnNavMesh)
        {
            Vector3 direction = destination - transform.position;
            direction.y = 0f;
            SetMovementIntent(direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero, sprint);
            return false;
        }

        navActive = true;
        navSprint = sprint;
        navAgent.isStopped = false;
        navAgent.stoppingDistance = stoppingDistance;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, brain.navSampleRadius, NavMesh.AllAreas)) navAgent.SetDestination(hit.position);
        else navAgent.SetDestination(destination);

        return true;
    }

    public void StopNavigation()
    {
        navActive = false;

        if (navAgent != null && navAgent.isOnNavMesh)
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
            SyncNavAgent();
        }

        StopMoving();
    }

    void ApplyMovementIntent(Vector3 worldDirection, bool sprint)
    {
        worldDirection.y = 0f;
        desiredMovementDirection = worldDirection.sqrMagnitude > 1f ? worldDirection.normalized : worldDirection;
        sprintInput = sprint;

        Vector3 local = transform.InverseTransformDirection(desiredMovementDirection);
        movementInput = new Vector2(Mathf.Clamp(local.x, -1f, 1f), Mathf.Clamp(local.z, -1f, 1f));
    }

    void UpdateStatePreview()
    {
        if (stateMachine == null) return;
        string newState = stateMachine.CurrentState != null ? stateMachine.CurrentState.GetType().Name : "None";
        if (newState == currentState) return;
        previousState = currentState;
        currentState = newState;
        statePreview = $"{previousState} -> {currentState}";
    }
}

public enum TutorialNPCLessonType
{
    MessageOnly,
    PlayerMustPickUpSword,
    PlayerMustHitNPC,
    PlayerMustHitBlockingNPC,
    PlayerMustBlockAttack,
    PlayerMustParryAttack,
    FinalFreeFight
}

[Serializable]
public class TutorialNPCLesson
{
    public TutorialNPCLessonType lessonType;
    public bool requireCompletion = true;

    [Header("Dialogue")]
    [TextArea(2, 5)] public string[] promptDialogue;
    [TextArea(2, 5)] public string[] successDialogue;
    [TextArea(2, 5)] public string[] failDialogue;

    [Header("Timing")]
    [Min(0f)] public float delayAfterDialogueOverride;
    [Min(0f)] public float repeatActionDelayOverride;

    [Header("Final Fight")]
    [Range(0.01f, 1f)] public float finalFightStopHealthPercent = 0.5f;
}
