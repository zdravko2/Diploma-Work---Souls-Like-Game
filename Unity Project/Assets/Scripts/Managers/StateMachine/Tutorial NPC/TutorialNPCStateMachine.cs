
using System.Collections.Generic;
using UnityEngine;

public class TutorialNPCStateMachine : StateMachine
{
    readonly TutorialNPCController controller;
    readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();
    readonly List<TutorialLessonState> lessonStates = new List<TutorialLessonState>();

    public readonly IdleState idle;
    public readonly IntroState intro;
    public readonly CompleteState complete;
    public readonly DeathState death;

    bool startRequested;
    AIAttackDefinition pendingAttack;
    float pendingUntil;
    float nextAttackDecisionTime;
    bool strafeRight;

    public bool IsTutorialRunning => CurrentState != idle && CurrentState != complete && CurrentState != death;
    TutorialNPCBrainSettings Brain => controller.Brain;
    CharacterManager Character => controller.character;
    CharacterManager Target => controller.TutorialTarget;
    bool HasTarget => controller.HasTutorialTarget;
    float TargetDistance => controller.DistanceToTarget;
    float TargetAngle => controller.SignedAngleToTarget;
    bool CanAct => controller.CanIssueAction && Character.combatManager.canAttack;
    bool HasPendingAttack => pendingAttack != null;
    bool PendingExpired => HasPendingAttack && Time.time >= pendingUntil;
    bool PendingReady => HasPendingAttack && controller.CanSeeTarget() && CanAct && controller.AttackFits(pendingAttack);
    float CombatDistance => Mathf.Max(Brain.preferredMaxDistance, GetLargestAttackDistance()) + Brain.distanceTolerance;

    public TutorialNPCStateMachine(TutorialNPCController controller)
    {
        this.controller = controller;

        idle = new IdleState(this);
        intro = new IntroState(this);
        complete = new CompleteState(this);
        death = new DeathState(this);

        for (int i = 0; i < controller.Lessons.Count; i++) lessonStates.Add(CreateLessonState(controller.Lessons[i], i));

        AddGlobalTransition(death, () => Character.statusManager.isDead, 100);

        State firstLesson = lessonStates.Count > 0 ? lessonStates[0] : complete;
        idle.AddTransition(intro, () => startRequested && controller.HasTutorialTarget, 10);
        intro.AddTransition(firstLesson, () => intro.Done && controller.HasTutorialTarget, 10);
        intro.AddTransition(idle, () => intro.Done && !controller.HasTutorialTarget, 0);

        for (int i = 0; i < lessonStates.Count; i++)
        {
            State next = i + 1 < lessonStates.Count ? lessonStates[i + 1] : complete;
            lessonStates[i].AddTransition(next, () => ((TutorialLessonState)CurrentState).Done, 10);
            lessonStates[i].AddTransition(idle, () => !controller.HasTutorialTarget, 0);
        }

        complete.AddTransition(idle, () => complete.Done, 0);
        SetInitialState(idle);
    }

    TutorialLessonState CreateLessonState(TutorialNPCLesson lesson, int index)
    {
        switch (lesson.lessonType)
        {
            case TutorialNPCLessonType.MessageOnly: return new MessageLessonState(this, lesson, index);
            case TutorialNPCLessonType.PlayerMustPickUpSword: return new PlayerMustPickUpSword(this, lesson, index);
            case TutorialNPCLessonType.PlayerMustHitNPC: return new PlayerHitNPCLessonState(this, lesson, index);
            case TutorialNPCLessonType.PlayerMustHitBlockingNPC: return new PlayerHitBlockingNPCLessonState(this, lesson, index);
            case TutorialNPCLessonType.PlayerMustBlockAttack: return new PlayerBlockAttackLessonState(this, lesson, index);
            case TutorialNPCLessonType.PlayerMustParryAttack: return new PlayerParryAttackLessonState(this, lesson, index);
            case TutorialNPCLessonType.FinalFreeFight: return new FinalFreeFightLessonState(this, lesson, index);
            default: return new MessageLessonState(this, lesson, index);
        }
    }

    public void RequestTutorialStart()
    {
        startRequested = true;
    }

    float GetLargestAttackDistance()
    {
        float result = 0f;
        for (int i = 0; i < controller.Actions.attacks.Count; i++)
        {
            AIAttackDefinition attack = controller.Actions.attacks[i];
            if (attack != null) result = Mathf.Max(result, attack.maxDistance);
        }
        return result;
    }

    bool Roll(float chance) => chance > 0f && UnityEngine.Random.value < Mathf.Clamp01(chance);
    bool ShouldBypass() => Brain.unpredictability > 0f && Roll(Brain.unpredictability);

    bool TryPickAttack(bool requireInRange)
    {
        pendingAttack = null;
        if (Brain.aggression <= 0f || !HasTarget || !CanAct || Time.time < nextAttackDecisionTime) return false;
        if (!Roll(Brain.aggression)) return false;

        bool ignoreCooldowns = ShouldBypass();
        List<AIAttackDefinition> options = new List<AIAttackDefinition>();
        float totalWeight = 0f;

        for (int i = 0; i < controller.Actions.attacks.Count; i++)
        {
            AIAttackDefinition attack = controller.Actions.attacks[i];
            if (attack == null || string.IsNullOrWhiteSpace(attack.animationName) || attack.weight <= 0f) continue;
            if (requireInRange && !controller.AttackFits(attack)) continue;
            if (!ignoreCooldowns && cooldowns.TryGetValue(attack.animationName, out float readyAt) && Time.time < readyAt) continue;

            options.Add(attack);
            totalWeight += attack.weight;
        }

        if (options.Count == 0 || totalWeight <= 0f) return false;

        float pick = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < options.Count; i++)
        {
            pick -= options[i].weight;
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

    bool TryPlayPendingAttack()
    {
        if (pendingAttack == null || !PendingReady) return false;
        bool played = controller.TryPlayAttack(pendingAttack);
        if (!played) return false;
        cooldowns[pendingAttack.animationName] = Time.time + Mathf.Max(0f, pendingAttack.cooldown);
        nextAttackDecisionTime = ShouldBypass() ? Time.time : Time.time + Brain.postAttackDecisionDelay;
        return true;
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

    void MoveToCombatRange()
    {
        float desiredDistance = Mathf.Lerp(Brain.preferredMaxDistance, Brain.preferredMinDistance, Mathf.Clamp01(Brain.aggression + Brain.pressure * 0.5f));
        controller.MoveToCombatRange(desiredDistance);
    }

    void MoveForPendingAttack()
    {
        controller.MoveToAttackRange(pendingAttack);
    }

    void StrafeTarget()
    {
        controller.StrafeTarget(strafeRight);
    }

    void RetreatFromTarget()
    {
        controller.RetreatFromTarget();
    }

    void TickCombatBrain()
    {
        if (!HasTarget)
        {
            controller.StopNavigation();
            return;
        }

        controller.FaceTarget();

        if (HasPendingAttack)
        {
            if (PendingReady)
            {
                controller.StopNavigation();
                TryPlayPendingAttack();
                ClearPendingAttack();
                return;
            }

            if (PendingExpired)
            {
                ClearPendingAttack();
                return;
            }

            MoveForPendingAttack();
            return;
        }

        if (!controller.FinishedAction()) return;

        if (ShouldRetreat())
        {
            RetreatFromTarget();
            return;
        }

        if (TargetDistance > CombatDistance)
        {
            MoveToCombatRange();
            TryPickAttack(false);
            return;
        }

        if (TryPickAttack(true)) return;

        if (UnityEngine.Random.value < Brain.mobility)
        {
            strafeRight = !strafeRight;
            StrafeTarget();
            return;
        }

        MoveToCombatRange();
    }

    void TickSingleAttackLesson(ref bool attackStarted, ref float nextAttackTime)
    {
        if (!HasTarget) return;
        controller.FaceTarget();

        if (attackStarted)
        {
            if (controller.FinishedAction()) attackStarted = false;
            return;
        }

        if (Time.time < nextAttackTime) return;

        AIAttackDefinition attack = controller.Actions.GetRandomAttack(TargetDistance, TargetAngle, false);
        if (attack == null) return;

        if (!controller.AttackFits(attack))
        {
            controller.MoveToAttackRange(attack);
            return;
        }

        if (!controller.TryPlayAttack(attack)) return;
        attackStarted = true;
        nextAttackTime = Time.time + Mathf.Max(0.1f, attack.cooldown);
    }

    void ProtectTeachingHealth(float npcHealth, float playerHealth)
    {
        if (Brain.protectNPCBeforeFinalFight && Character.statusManager.currentHealth < npcHealth) Character.statusManager.RegainHealth(1000);
        if (Brain.protectPlayerBeforeFinalFight && Target != null && Target.statusManager.currentHealth < playerHealth) Target.statusManager.RegainHealth(1000);
    }

    public abstract class TutorialState : State
    {
        protected readonly TutorialNPCStateMachine machine;
        protected TutorialState(TutorialNPCStateMachine machine) : base(machine) => this.machine = machine;
    }

    public sealed class IdleState : TutorialState
    {
        public IdleState(TutorialNPCStateMachine machine) : base(machine) { }

        public override void Enter()
        {
            machine.startRequested = false;
            machine.controller.StopNavigation();
            machine.controller.CloseDialogue();
            machine.controller.EndHeldBlock();
        }
    }

    public sealed class IntroState : TutorialState
    {
        public bool Done { get; private set; }
        public IntroState(TutorialNPCStateMachine machine) : base(machine) { }

        public override void Enter()
        {
            Done = false;
            machine.startRequested = false;
            machine.controller.StopNavigation();
            machine.controller.ShowDialogueLines(machine.Brain.openingDialogue);
        }

        public override void Tick()
        {
            machine.controller.FaceTarget();
            Done = machine.controller.DialogueComplete;
        }
    }

    public abstract class TutorialLessonState : TutorialState
    {
        protected readonly TutorialNPCLesson lesson;
        protected readonly int index;
        protected float delayUntil;
        protected float startingNPCHealth;
        protected float startingPlayerHealth;
        protected bool started;
        protected bool resultStarted;
        public bool Done { get; private set; }

        protected TutorialLessonState(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine)
        {
            this.lesson = lesson;
            this.index = index;
        }

        public override void Enter()
        {
            Done = false;
            started = false;
            resultStarted = false;
            startingNPCHealth = machine.Character.statusManager.currentHealth;
            startingPlayerHealth = machine.Target != null ? machine.Target.statusManager.currentHealth : 0f;
            machine.controller.EndHeldBlock();
            machine.ClearPendingAttack();
            machine.controller.StopNavigation();
            machine.controller.ShowDialogueLines(lesson.promptDialogue);
        }

        public override void Tick()
        {
            if (Done) return;
            machine.controller.FaceTarget();

            if (!machine.controller.DialogueComplete) return;

            if (!started)
            {
                started = true;
                delayUntil = Time.time + DelayAfterDialogue();
                machine.controller.CloseDialogue();
                OnLessonStart();
                return;
            }

            if (resultStarted)
            {
                if (machine.controller.DialogueComplete) Done = true;
                return;
            }

            if (Time.time < delayUntil) return;

            TickLesson();
            if (lesson.lessonType != TutorialNPCLessonType.FinalFreeFight) machine.ProtectTeachingHealth(startingNPCHealth, startingPlayerHealth);
        }

        public override void Exit()
        {
            machine.controller.EndHeldBlock();
        }

        protected virtual void OnLessonStart() { }
        protected abstract void TickLesson();

        protected void Complete(bool success)
        {
            if (!success && lesson.requireCompletion) return;

            if (!resultStarted)
            {
                resultStarted = true;
                string[] result = success ? lesson.successDialogue : lesson.failDialogue;
                if (result != null && result.Length > 0)
                {
                    machine.controller.ShowDialogueLines(result);
                    return;
                }
            }

            if (machine.controller.DialogueComplete) Done = true;
        }

        protected float DelayAfterDialogue()
        {
            return lesson.delayAfterDialogueOverride > 0f ? lesson.delayAfterDialogueOverride : machine.Brain.delayAfterDialogue;
        }

        protected float RepeatActionDelay()
        {
            return lesson.repeatActionDelayOverride > 0f ? lesson.repeatActionDelayOverride : machine.Brain.repeatAttackDelay;
        }
    }

    public sealed class MessageLessonState : TutorialLessonState
    {
        public MessageLessonState(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine, lesson, index) { }
        protected override void OnLessonStart()
        {
            PlayerUIManager.Instance.ToggleHUD(true);
        }
        protected override void TickLesson() => Complete(true);
    }

    public sealed class PlayerMustPickUpSword : TutorialLessonState
    {
        public PlayerMustPickUpSword(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine, lesson, index) { }

        protected override void TickLesson()
        {
            if (machine.Target.inventoryManager.GetByType<WeaponItem>() != null && Vector3.Distance(machine.Character.transform.position, machine.Target.transform.position) < 1.5f) Complete(true);
        }
    }

    public sealed class PlayerHitNPCLessonState : TutorialLessonState
    {
        public PlayerHitNPCLessonState(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine, lesson, index) { }

        protected override void TickLesson()
        {
            machine.controller.StopNavigation();
            if (machine.Character.statusManager.currentHealth < startingNPCHealth) Complete(true);
        }
    }

    public sealed class PlayerHitBlockingNPCLessonState : TutorialLessonState
    {
        public PlayerHitBlockingNPCLessonState(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine, lesson, index) { }

        protected override void OnLessonStart()
        {
            machine.controller.StartHeldBlock();
        }

        protected override void TickLesson()
        {
            machine.controller.StopNavigation();
            machine.controller.StartHeldBlock();
            if (machine.Character.combatManager.isHitReacting && machine.Character.combatManager.isBlocking) Complete(true);
            else if (machine.Character.statusManager.currentHealth < startingNPCHealth) Complete(true);
        }
    }

    public sealed class PlayerBlockAttackLessonState : TutorialLessonState
    {
        bool attackStarted;
        float nextAttackTime;

        public PlayerBlockAttackLessonState(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine, lesson, index) { }

        protected override void OnLessonStart()
        {
            attackStarted = false;
            nextAttackTime = 0f;
        }

        protected override void TickLesson()
        {
            if (machine.Target != null && machine.Target.combatManager.isHitReacting && machine.Target.combatManager.isBlocking)
            {
                Complete(true);
                return;
            }

            if (machine.controller.DialogueComplete) machine.TickSingleAttackLesson(ref attackStarted, ref nextAttackTime);
        }
    }

    public sealed class PlayerParryAttackLessonState : TutorialLessonState
    {
        bool attackStarted;
        float nextAttackTime;

        public PlayerParryAttackLessonState(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine, lesson, index) { }

        protected override void OnLessonStart()
        {
            attackStarted = false;
            nextAttackTime = 0f;
        }

        protected override void TickLesson()
        {
            if (machine.Character.combatManager.isExposed || machine.Character.combatManager.isStunned)
            {
                Complete(true);
                return;
            }

            if (machine.controller.DialogueComplete) machine.TickSingleAttackLesson(ref attackStarted, ref nextAttackTime);
        }
    }

    public sealed class FinalFreeFightLessonState : TutorialLessonState
    {
        float stopHealth;

        public FinalFreeFightLessonState(TutorialNPCStateMachine machine, TutorialNPCLesson lesson, int index) : base(machine, lesson, index) { }

        protected override void OnLessonStart()
        {
            machine.Character.statusManager.InitializeStats(
                machine.Character.statusManager.vitality,
                machine.Character.statusManager.endurance,
                machine.Character.statusManager.mind,
                machine.Character.statusManager.strength + 1,
                machine.Character.statusManager.dexterity
            );

            float percent = lesson.finalFightStopHealthPercent > 0f ? lesson.finalFightStopHealthPercent : machine.Brain.finalFightStopHealthPercent;
            stopHealth = machine.Character.statusManager.currentHealth * Mathf.Clamp01(percent);
        }

        protected override void TickLesson()
        {
            if (machine.Character.statusManager.currentHealth <= stopHealth)
            {
                machine.controller.StopNavigation();
                Complete(true);
                return;
            }

            machine.TickCombatBrain();
        }
    }

    public sealed class CompleteState : TutorialState
    {
        public bool Done { get; private set; }
        public CompleteState(TutorialNPCStateMachine machine) : base(machine) { }

        public override void Enter()
        {
            Done = false;
            machine.controller.StopNavigation();
            machine.controller.EndHeldBlock();
            machine.controller.ShowDialogueLines(machine.Brain.completionDialogue);
        }

        public override void Tick()
        {
            machine.controller.FaceTarget();
            if (!machine.controller.DialogueComplete) return;
            machine.controller.EndTutorial();
            Done = true;

            // PLAY CUTSCENE HERE
            LillianKidnap_CutsceneDirector.Instance.Play();
        }
    }

    public sealed class DeathState : TutorialState
    {
        public DeathState(TutorialNPCStateMachine machine) : base(machine) { }

        public override void Enter()
        {
            machine.controller.EndTutorial();
            machine.ClearPendingAttack();
        }
    }
}