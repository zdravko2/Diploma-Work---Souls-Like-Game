using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAIController : CharacterControlsManager
{
    [Header("Hitboxes")]
    public HitBox rightHand;
    public HitBox leftHand;
    public HitBox rightFoot;
    public HitBox leftFoot;

    [Header("AI")]
    public string statePreview;
    private string currentState;
    private string previousState;
    public AIBrainSettings brain = new AIBrainSettings();
    [SerializeField, Min(0)] int currentPhase;
    [Header("Navigation")]
    [SerializeField] bool useNavMesh = true;
    [SerializeField] NavMeshAgent navAgent;
    [SerializeField, Min(0f)] float navSampleRadius = 2f;
    [SerializeField, Min(0f)] float navLinkCompleteDistance = 0.25f;

    bool navActive;
    bool navSprint;
    
    [Header("Combat")]
    [SerializeField] List<AIAttackDefinition> attacks = new List<AIAttackDefinition>();
    public List<CharacterManager> minions = new List<CharacterManager>();

    EnemyAIStateMachine stateMachine;
    float nextTargetRefreshTime;
    Vector3 homePosition;

    public AIBrainSettings Brain => brain;
    public List<AIAttackDefinition> Attacks => attacks;
    public int CurrentPhase => currentPhase;
    public Vector3 HomePosition => homePosition;
    public CharacterManager Target => character.combatManager.lockedOnTarget;
    public bool HasTarget => character.combatManager.isLockedOn;
    public float DistanceToTarget => HasTarget ? Vector3.Distance(transform.position, Target.transform.position) : Mathf.Infinity;
    public bool CanIssueAction =>
        !character.statusManager.isDead &&
        !character.animatorManager.isInAnimation;

    public Vector3 DirectionToTarget
    {
        get
        {
            if (!HasTarget) return Vector3.zero;
            Vector3 direction = Target.transform.position - transform.position;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }

    public float SignedAngleToTarget => HasTarget ? Vector3.SignedAngle(transform.forward, DirectionToTarget, Vector3.up) : 0f;

    protected override void Start()
    {
        base.Start();
        homePosition = transform.position;
        SetupHitboxes();
        SetupNavMesh();
        stateMachine = new EnemyAIStateMachine(this);
        
        DontDestroyOnLoad(gameObject);
    }

    protected override void Update()
    {
        if (character.statusManager.isDead) return;

        if (character.animatorManager.isPerformingAction) character.animatorManager.UpdateAnimatorMovementParameters(0, 0, 0);

        currentPhase = character.statusManager.currentHealth > character.statusManager.maxHealth * 0.6f ? 0 : 1;
        RefreshTarget();
        stateMachine?.Tick();
        UpdateNavMeshMovementIntent();
        UpdateMovementParameters();
        UpdateStatePreview();
        CheckMinionStatus();
        SetHyperArmor();
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

    float timeSinceHyperArmor = 0f;

    void SetHyperArmor()
    {
        if (stateMachine.CurrentState is EnemyAIStateMachine.RootState ||
            stateMachine.CurrentState is EnemyAIStateMachine.SpawnState ||
            stateMachine.CurrentState is EnemyAIStateMachine.AwakeState)
        {
            character.combatManager.hasHyperArmor = false;
            return;
        }

        if (character.combatManager.hasHyperArmor) return;

        float delta = Time.deltaTime;

        if (character.combatManager.isHitReacting) delta *= 2f;

        timeSinceHyperArmor += delta;

        if (timeSinceHyperArmor >= brain.hyperArmorCooldown)
        {
            character.combatManager.hasHyperArmor = true;
            timeSinceHyperArmor = 0f;
        }
    }

    void CheckMinionStatus()
    {
        if (character.statusManager.isDead)
        {
            for (int i = minions.Count - 1; i >= 0; i--)
            {
                if (minions[i] != null && !minions[i].statusManager.isDead)
                {
                    minions[i].statusManager.InflictDamage(new DamageData { physical = 9999, attacker = character });
                }
            }
        }
        character.animatorManager.animator.SetBool("HasMinions", minions.Count > 0);

        for (int i = minions.Count - 1; i >= 0; i--)
        {
            if (minions[i] == null || minions[i].statusManager.isDead)
            {
                minions.RemoveAt(i);
            }
        }
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

    public bool TryPlayActionAnimation(string animationName, bool applyRootMotion = true, bool canBeInterrupted = false, float crossFadeDuration = 0.2f)
    {
        if (!CanIssueAction || string.IsNullOrWhiteSpace(animationName)) return false;
        StopMoving();
        character.animatorManager.PlayTargetActionAnimation(animationName, applyRootMotion: applyRootMotion, canBeInterrupted: canBeInterrupted, canMove: false, canRotate: false, crossFadeDuration: crossFadeDuration);
        return true;
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

    void RefreshTarget(bool force = false)
    {
        if (!force && Time.time < nextTargetRefreshTime) return;
        nextTargetRefreshTime = Time.time + brain.targetRefreshInterval;

        if (character.statusManager.isDead)
        {
            character.combatManager.SetLockOnTarget(null);
            return;
        }

        CharacterManager target = character.combatManager.lockedOnTarget;
        bool refresh = target == null || target.statusManager.isDead;

        if (!refresh && target != null)
        {
            float distance = Vector3.Distance(transform.position, target.transform.position);
            refresh = distance > character.combatManager.lockOnRadius * 1.5f;
        }

        if (!refresh && !brain.keepTargetLockOn) refresh = true;
        if (refresh) character.combatManager.SetLockOnTarget(character.combatManager.FindTarget());
    }

    public bool CanSeeTarget()
    {
        if (!HasTarget) return false;

        Vector3 start = transform.position + Vector3.up * 1.5f;
        Vector3 end = Target.transform.position + Vector3.up * 1.5f;
        Vector3 direction = end - start;

        return !Physics.Raycast(
            start,
            direction.normalized,
            direction.magnitude,
            GameManager.Instance.environmentLayer);
    }

    void SetupHitboxes()
    {
        if (character.inventoryManager != null && character.inventoryManager.GetByType<WeaponItem>() is WeaponItem weaponItem)
        {
            character.equipmentManager.AssignWeaponToMainHand(weaponItem);
            character.equipmentManager.EquipMainHandWeapon();
        }

        Register(rightHand);
        Register(leftHand);
        Register(rightFoot);
        Register(leftFoot);
    }

    void Register(HitBox hitBox)
    {
        if (hitBox == null) return;
        hitBox.owner = character;
        hitBox.baseDamage = new DamageData { physical = 1, poise = 1, attacker = character };
        character.combatManager.HandleHitBoxRegistration(hitBox, true);
    }

    void UpdateStatePreview()
    {
        if (stateMachine == null) return;
        string newState = stateMachine.CurrentState != null ? stateMachine.CurrentState.GetType().Name : "None";
        if (newState != currentState)
        {
            previousState = currentState;
            currentState = newState;
            statePreview = $"{previousState} -> {currentState}";
        }
    }

    public void SetupNavMesh()
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
        while (!NavMesh.SamplePosition(transform.position, out hit, navSampleRadius, NavMesh.AllAreas))
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

        if (direction.magnitude <= navLinkCompleteDistance)
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
            SetMovementIntent(direction.normalized, sprint);
            return false;
        }

        navActive = true;
        navSprint = sprint;

        navAgent.isStopped = false;
        navAgent.stoppingDistance = stoppingDistance;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            navAgent.SetDestination(hit.position);
        else
            navAgent.SetDestination(destination);

        return true;
    }

    public bool TryGetNavPoint(Vector3 point, out Vector3 result)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = point;
        return false;
    }

    public bool TryGetReachableNavPoint(Vector3 point, out Vector3 result)
    {
        result = point;

        if (navAgent == null || !navAgent.isOnNavMesh)
            return false;

        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            return false;

        NavMeshPath path = new NavMeshPath();

        if (!navAgent.CalculatePath(hit.position, path))
            return false;

        if (path.status != NavMeshPathStatus.PathComplete)
            return false;

        result = hit.position;
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
}
