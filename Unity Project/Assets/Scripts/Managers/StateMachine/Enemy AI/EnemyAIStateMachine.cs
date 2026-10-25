using System.Collections.Generic;
using UnityEngine;

public class EnemyAIStateMachine : StateMachine
{
    readonly EnemyAIController controller;
    readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();

    public readonly RootState root;
    public readonly SpawnState spawn;
    public readonly AwakeState awake;
    public readonly IdleState idle;
    public readonly PatrolState patrol;
    public readonly ChaseState chase;
    public readonly StrafeState strafe;
    public readonly AttackSelectionState attackSelection;
    public readonly AttackState attack;
    public readonly RetreatState retreat;
    //public readonly StaggerState stagger;
    public readonly DeathState death;

    AIAttackDefinition pendingAttack;
    Vector3 patrolPoint;
    float pendingUntil;
    float nextAttackDecisionTime;
    bool strafeRight;

    public EnemyAIStateMachine(EnemyAIController controller)
    {
        this.controller = controller;

        root = new RootState(this); // Delay initialization of spawn and intro states until Start to ensure proper timing of animations
        spawn = new SpawnState(this);
        awake = new AwakeState(this);
        idle = new IdleState(this);
        patrol = new PatrolState(this);
        chase = new ChaseState(this);
        strafe = new StrafeState(this);
        attackSelection = new AttackSelectionState(this);
        attack = new AttackState(this);
        retreat = new RetreatState(this);
        //stagger = new StaggerState(this);
        death = new DeathState(this);

        AddGlobalTransition(death, () => Character.statusManager.isDead, 100);
        //AddGlobalTransition(stagger, () => Character.combatManager.isStunned && CurrentState != stagger && CurrentState != death, 90);

        root.AddTransition(spawn, () => true, 0);
        spawn.AddTransition(idle, () => !Brain.awaitEnemyBeforeActing, 10);
        spawn.AddTransition(idle, () => Character.combatManager.isHitReacting, 10);
        spawn.AddTransition(awake, () => spawn.Done && HasTarget && !Character.combatManager.isHitReacting, 10);
        awake.AddTransition(chase, () => awake.Done && HasTarget, 10);
        awake.AddTransition(idle, () => awake.Done, 0);

        idle.AddTransition(chase, () => HasTarget, 10);
        idle.AddTransition(patrol, () => idle.Done, 0);

        patrol.AddTransition(chase, () => HasTarget, 10);
        patrol.AddTransition(idle, () => patrol.Done, 0);

        chase.AddTransition(attackSelection, () => chase.WantsAttack, 20);
        chase.AddTransition(retreat, () => chase.WantsRetreat, 0);
        chase.AddTransition(strafe, () => chase.WantsStrafe, -10);
        chase.AddTransition(idle, () => !HasTarget, -20);

        strafe.AddTransition(attackSelection, () => strafe.WantsAttack, 20);
        strafe.AddTransition(chase, () => strafe.WantsChase, 0);
        strafe.AddTransition(retreat, () => strafe.WantsRetreat, -10);
        strafe.AddTransition(idle, () => !HasTarget, -20);

        attackSelection.AddTransition(attack, () => attackSelection.Ready, 20);
        attackSelection.AddTransition(chase, () => attackSelection.WantsChase, 0);
        attackSelection.AddTransition(strafe, () => attackSelection.WantsStrafe, -10);
        attackSelection.AddTransition(idle, () => !HasTarget, -20);

        attack.AddTransition(attackSelection, () => attack.WantsChain, 20);
        attack.AddTransition(chase, () => attack.WantsChase, 0);
        attack.AddTransition(retreat, () => attack.WantsRetreat, -10);
        attack.AddTransition(strafe, () => attack.WantsStrafe, -20);
        attack.AddTransition(idle, () => attack.Done && !HasTarget, -30);

        retreat.AddTransition(chase, () => retreat.WantsChase, 0);
        retreat.AddTransition(strafe, () => retreat.Done, -10);
        retreat.AddTransition(idle, () => !HasTarget, -20);

        //stagger.AddTransition(chase, () => stagger.Done && HasTarget && TargetDistance > CombatDistance, 10);
        //stagger.AddTransition(strafe, () => stagger.Done && HasTarget, 0);
        //stagger.AddTransition(idle, () => stagger.Done, -10);

        SetInitialState(root);
    }

    CharacterManager Character => controller.character;
    AIBrainSettings Brain => controller.Brain;
    bool HasTarget => controller.HasTarget;
    float TargetDistance => controller.DistanceToTarget;
    Vector3 TargetDirection => controller.DirectionToTarget;
    float TargetAngle => controller.SignedAngleToTarget;
    bool CanAct => controller.CanIssueAction && Character.combatManager.canAttack;
    bool CanDecideAttack => Brain.aggression > 0f && HasTarget && CanAct && Time.time >= nextAttackDecisionTime;
    bool HasPendingAttack => pendingAttack != null;
    bool PendingExpired => HasPendingAttack && Time.time >= pendingUntil;
    bool PendingReady => HasPendingAttack && controller.CanSeeTarget() && CanAct && AttackFits(pendingAttack);
    float CombatDistance
    {
        get
        {
            float value = Brain.preferredMaxDistance;
            for (int i = 0; i < controller.Attacks.Count; i++)
            {
                AIAttackDefinition data = controller.Attacks[i];
                if (data != null && data.requiredPhase <= controller.CurrentPhase) value = Mathf.Max(value, data.maxDistance);
            }
            return value + Brain.distanceTolerance;
        }
    }

    bool Roll(float chance) => chance > 0f && Random.value < Mathf.Clamp01(chance);
    bool ShouldBypass() => Brain.unpredictability > 0f && Roll(Brain.unpredictability);

    bool FinishedAction()
    {
        return !Character.animatorManager.isPerformingAction &&
               !Character.animatorManager.isInAnimation &&
               !Character.combatManager.isAttacking &&
               !Character.combatManager.isHitReacting &&
               !Character.combatManager.isStunned;
    }

    bool AttackFits(AIAttackDefinition data)
    {
        if (data == null) return false;
        return TargetDistance >= data.minDistance - Brain.distanceTolerance &&
               TargetDistance <= data.maxDistance + Brain.distanceTolerance &&
               TargetAngle >= data.minAngle &&
               TargetAngle <= data.maxAngle;
    }

    bool TryPickAttack(bool requireInRange)
    {
        pendingAttack = null;
        if (!CanDecideAttack || !Roll(Brain.aggression)) return false;

        bool ignoreCooldowns = ShouldBypass();
        bool ignoreWeights = ShouldBypass();
        List<AIAttackDefinition> options = new List<AIAttackDefinition>();
        float total = 0f;

        for (int i = 0; i < controller.Attacks.Count; i++)
        {
            AIAttackDefinition data = controller.Attacks[i];
            if (data == null || string.IsNullOrWhiteSpace(data.animationName) || data.requiredPhase > controller.CurrentPhase || data.weight <= 0f) continue;
            if (requireInRange && !AttackFits(data)) continue;
            if (!ignoreCooldowns && cooldowns.TryGetValue(data.animationName, out float readyAt) && Time.time < readyAt) continue;
            options.Add(data);
            total += ignoreWeights ? 1f : data.weight;
        }

        if (options.Count == 0 || total <= 0f) return false;

        float pick = Random.value * total;
        for (int i = 0; i < options.Count; i++)
        {
            pick -= ignoreWeights ? 1f : options[i].weight;
            if (pick > 0f) continue;
            pendingAttack = options[i];
            pendingUntil = Time.time + Mathf.Max(0.1f, pendingAttack.positionTimeout);
            return true;
        }

        pendingAttack = options[options.Count - 1];
        pendingUntil = Time.time + Mathf.Max(0.1f, pendingAttack.positionTimeout);
        return true;
    }

    bool TryChainAttack()
    {
        return Brain.aggression > 0f && TargetDistance <= CombatDistance && TryPickAttack(false);
    }

    void CommitAttack()
    {
        if (pendingAttack == null) return;
        cooldowns[pendingAttack.animationName] = Time.time + Mathf.Max(0f, pendingAttack.cooldown);
        nextAttackDecisionTime = ShouldBypass() ? Time.time : Time.time + Brain.postAttackDecisionDelay;
    }

    void ClearPendingAttack()
    {
        pendingAttack = null;
        pendingUntil = 0f;
    }

    bool ShouldRetreat()
    {
        if (!HasTarget || HasPendingAttack) return false;
        if (TargetDistance > Brain.preferredMinDistance * 0.75f) return false;
        return Roll(Mathf.Clamp01(Brain.caution * (1f - Brain.aggression)));
    }

    bool ShouldRetreatAfterAttack()
    {
        if (!HasTarget || HasPendingAttack) return false;
        if (TargetDistance > Brain.preferredMinDistance * 0.7f) return false;
        return Roll(Mathf.Clamp01(Brain.caution * (1f - Brain.aggression * 0.8f)));
    }

    void MoveToPatrolPoint()
    {
        controller.SetNavDestination(patrolPoint, false, Brain.patrolPointTolerance);
    }

    bool ReachedPatrolPoint()
    {
        Vector3 a = Character.transform.position;
        Vector3 b = patrolPoint;
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b) <= Brain.patrolPointTolerance;
    }

    bool PickPatrolPoint()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * Brain.patrolRadius;
            Vector3 point = controller.HomePosition + new Vector3(offset.x, 0f, offset.y);

            if (controller.TryGetReachableNavPoint(point, out Vector3 navPoint))
            {
                patrolPoint = navPoint;
                return true;
            }
        }

        patrolPoint = Character.transform.position;
        return false;
    }

    void MoveToCombatRange()
    {
        if (!HasTarget)
        {
            controller.StopNavigation();
            return;
        }

        float desiredDistance = Mathf.Lerp(
            Brain.preferredMaxDistance,
            Brain.preferredMinDistance,
            Mathf.Clamp01(Brain.aggression + Brain.pressure * 0.5f));

        Vector3 destination = controller.Target.transform.position - TargetDirection * desiredDistance;

        bool sprint =
            TargetDistance > Brain.chaseSprintDistance ||
            Brain.mobility + Brain.pressure + Brain.aggression > 1.2f;

        controller.SetNavDestination(destination, sprint, Brain.distanceTolerance);
    }

    void MoveForPendingAttack()
    {
        if (!HasTarget || pendingAttack == null)
        {
            controller.StopNavigation();
            return;
        }

        Vector3 destination = controller.Target.transform.position - TargetDirection * pendingAttack.idealDistance;

        bool sprint =
            TargetDistance > Brain.chaseSprintDistance ||
            Brain.mobility + Brain.aggression + Brain.pressure > 1.1f;

        controller.SetNavDestination(destination, sprint, Brain.distanceTolerance);
    }

    void StrafeTarget()
    {
        if (!HasTarget)
        {
            controller.StopNavigation();
            return;
        }

        Vector3 tangent = Vector3.Cross(Vector3.up, TargetDirection) * (strafeRight ? 1f : -1f);
        Vector3 center = controller.Target.transform.position - TargetDirection * Brain.preferredMinDistance;
        Vector3 destination = center + tangent * Mathf.Lerp(0.75f, 1.5f, Brain.mobility);

        controller.SetNavDestination(destination, false, Brain.distanceTolerance);
    }

    void RetreatFromTarget()
    {
        if (!HasTarget)
        {
            controller.StopNavigation();
            return;
        }

        Vector3 destination = Character.transform.position - TargetDirection * Brain.retreatDistance;
        bool sprint = Brain.mobility > 0.35f || Brain.caution > 0.5f;

        controller.SetNavDestination(destination, sprint, Brain.distanceTolerance);
    }

    public abstract class EnemyState : State
    {
        protected readonly EnemyAIStateMachine machine;
        protected EnemyState(EnemyAIStateMachine machine) : base(machine) => this.machine = machine;
    }

    public sealed class RootState : EnemyState
    {
        public RootState(EnemyAIStateMachine machine) : base(machine) { }
    }

    public sealed class SpawnState : EnemyState
    {
        public bool Done { get; private set; }
        public SpawnState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            Done = false;
            machine.controller.StopNavigation();
            machine.controller.TryPlayActionAnimation("Spawn", applyRootMotion: true, crossFadeDuration: 0f);
        }
        public override void Tick() => Done = machine.FinishedAction();
    }

    public sealed class AwakeState : EnemyState
    {
        public bool Done { get; private set; }
        public AwakeState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            Done = false;
            machine.controller.StopNavigation();
            machine.controller.TryPlayActionAnimation("Awake", applyRootMotion: true);
        }
        public override void Tick() => Done = machine.FinishedAction();
    }

    public sealed class IdleState : EnemyState
    {
        float endTime;
        public bool Done { get; private set; }
        public IdleState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            Done = false;
            endTime = Time.time + Random.Range(machine.Brain.idleDurationMin, machine.Brain.idleDurationMax);
            machine.controller.StopNavigation();
        }
        public override void Tick() => Done = Time.time >= endTime;
    }

    public sealed class PatrolState : EnemyState
    {
        float timeoutTime;
        bool hasPoint;

        public bool Done { get; private set; }

        public PatrolState(EnemyAIStateMachine machine) : base(machine) { }

        public override void Enter()
        {
            Done = false;
            timeoutTime = Time.time + machine.Brain.patrolTimeout;

            hasPoint = machine.PickPatrolPoint();

            if (hasPoint)
                machine.MoveToPatrolPoint();
            else
                Done = true;
        }

        public override void Tick()
        {
            if (!hasPoint)
            {
                Done = true;
                return;
            }

            machine.MoveToPatrolPoint();

            if (machine.ReachedPatrolPoint() || Time.time >= timeoutTime)
            {
                machine.controller.StopNavigation();
                Done = true;
            }
        }

        public override void Exit()
        {
            machine.controller.StopNavigation();
        }
    }

    public sealed class ChaseState : EnemyState
    {
        float nextThinkTime;
        public bool WantsAttack { get; private set; }
        public bool WantsStrafe { get; private set; }
        public bool WantsRetreat { get; private set; }
        public ChaseState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            WantsAttack = false;
            WantsStrafe = false;
            WantsRetreat = false;
            nextThinkTime = 0f;
        }
        public override void Tick()
        {
            WantsAttack = false;
            WantsStrafe = false;
            WantsRetreat = false;

            if (!machine.HasTarget)
            {
                machine.controller.StopNavigation();
                return;
            }

            machine.MoveToCombatRange();
            if (Time.time < nextThinkTime) return;

            nextThinkTime = Time.time + 0.12f;
            WantsAttack = machine.TryPickAttack(false);
            WantsRetreat = !WantsAttack && machine.ShouldRetreat();
            WantsStrafe = !WantsAttack && !WantsRetreat && machine.TargetDistance <= machine.CombatDistance;
        }
    }

    public sealed class StrafeState : EnemyState
    {
        float endTime;
        float nextThinkTime;
        public bool WantsAttack { get; private set; }
        public bool WantsRetreat { get; private set; }
        public bool WantsChase { get; private set; }
        public StrafeState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            WantsAttack = false;
            WantsRetreat = false;
            WantsChase = false;
            machine.strafeRight = !machine.strafeRight;
            endTime = Time.time + Random.Range(machine.Brain.strafeDurationMin, machine.Brain.strafeDurationMax);
            nextThinkTime = 0f;
        }
        public override void Tick()
        {
            WantsAttack = false;
            WantsRetreat = false;
            WantsChase = false;

            if (!machine.HasTarget)
            {
                machine.controller.StopNavigation();
                return;
            }

            machine.StrafeTarget();
            if (Time.time < nextThinkTime) return;

            nextThinkTime = Time.time + Mathf.Lerp(0.08f, 0.16f, 1f - machine.Brain.mobility);
            WantsAttack = machine.TryPickAttack(false);
            WantsRetreat = !WantsAttack && machine.ShouldRetreat();
            WantsChase = !WantsAttack && !WantsRetreat && (machine.TargetDistance > machine.CombatDistance || Time.time >= endTime);
        }
    }

    public sealed class AttackSelectionState : EnemyState
    {
        public bool Ready { get; private set; }
        public bool WantsStrafe { get; private set; }
        public bool WantsChase { get; private set; }
        public AttackSelectionState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            Ready = false;
            WantsStrafe = false;
            WantsChase = false;
            if (!machine.HasPendingAttack) machine.TryPickAttack(false);
        }
        public override void Tick()
        {
            Ready = false;
            WantsStrafe = false;
            WantsChase = false;

            if (!machine.HasTarget) return;
            if (!machine.HasPendingAttack)
            {
                WantsChase = true;
                return;
            }

            Ready = machine.PendingReady;
            if (Ready)
            {
                machine.controller.StopNavigation();
                return;
            }

            if (machine.PendingExpired)
            {
                machine.ClearPendingAttack();
                WantsChase = machine.TargetDistance > machine.CombatDistance;
                WantsStrafe = !WantsChase;
                return;
            }

            machine.MoveForPendingAttack();
        }
        public override void Exit()
        {
            if (!Ready) machine.ClearPendingAttack();
        }
    }

    public sealed class AttackState : EnemyState
    {
        bool started;
        public bool Done { get; private set; }
        public bool WantsChain { get; private set; }
        public bool WantsChase { get; private set; }
        public bool WantsRetreat { get; private set; }
        public bool WantsStrafe { get; private set; }
        public AttackState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            started = machine.HasPendingAttack && machine.PendingReady && machine.controller.TryPlayAttack(machine.pendingAttack);
            Done = !started;
            WantsChain = false;
            WantsChase = false;
            WantsRetreat = false;
            WantsStrafe = false;
            if (!started) machine.ClearPendingAttack();
        }
        public override void Tick()
        {
            if (Done) return;
            if (!machine.FinishedAction()) return;

            machine.CommitAttack();
            machine.ClearPendingAttack();
            Done = true;
            WantsChain = machine.TryChainAttack();
            WantsRetreat = !WantsChain && machine.ShouldRetreatAfterAttack();
            WantsChase = !WantsChain && !WantsRetreat && machine.HasTarget && machine.TargetDistance > machine.CombatDistance;
            WantsStrafe = !WantsChain && !WantsRetreat && !WantsChase && machine.HasTarget;
        }
    }

    public sealed class RetreatState : EnemyState
    {
        float endTime;
        public bool Done { get; private set; }
        public bool WantsChase { get; private set; }
        public RetreatState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            Done = false;
            WantsChase = false;
            endTime = Time.time + 0.75f;
        }
        public override void Tick()
        {
            Done = false;
            WantsChase = false;
            if (!machine.HasTarget)
            {
                machine.controller.StopNavigation();
                return;
            }

            if (machine.TargetDistance >= machine.Brain.retreatDistance || Time.time >= endTime)
            {
                machine.controller.StopNavigation();
                Done = true;
                WantsChase = machine.TargetDistance > machine.CombatDistance;
                return;
            }

            machine.RetreatFromTarget();
        }
    }

    public sealed class StaggerState : EnemyState
    {
        public bool Done { get; private set; }
        public StaggerState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            Done = false;
            machine.controller.StopNavigation();
            machine.ClearPendingAttack();
        }
        public override void Tick()
        {
            machine.controller.StopNavigation();
            Done = !machine.Character.combatManager.isStunned && machine.FinishedAction();
        }
    }

    public sealed class DeathState : EnemyState
    {
        public DeathState(EnemyAIStateMachine machine) : base(machine) { }
        public override void Enter()
        {
            machine.controller.StopNavigation();
            machine.ClearPendingAttack();
        }
    }
}
