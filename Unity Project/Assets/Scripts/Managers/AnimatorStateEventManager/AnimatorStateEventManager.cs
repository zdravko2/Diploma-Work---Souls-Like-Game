using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AnimatorStateEventManager : StateMachineBehaviour
{
    [SerializeField] public string statePath = "Unknown State";
    [SerializeField] bool catchUpOnEnter = true;
    [SerializeField] bool resetIfTimeGoesBackwards = true;
    [SerializeField] float backwardsEpsilon = 0.0005f;

    [SerializeField] bool limitLoopCatchUp = false;
    [SerializeField] int maxLoopsPerUpdate = 64;

    [SerializeReference]
    public List<AnimatorStateEvent> events = new List<AnimatorStateEvent>();

    const float CompletionThreshold = 0.999f;
    const float SegmentEpsilon = 0.000001f;

    struct EventRuntime
    {
        public bool triggeredThisState;
        public int lastTriggeredLoop;
    }

    sealed class Runtime
    {
        public float lastNormalized;
        public int enterFrame;
        public int lastUpdateFrame;
        public bool interrupted;
        public EventRuntime[] ev;

        public Runtime(int eventCount)
        {
            ev = CreateEventRuntimeArray(eventCount);
            lastNormalized = 0f;
            enterFrame = -1;
            lastUpdateFrame = -1;
            interrupted = false;
        }
    }

    struct PendingTrigger
    {
        public int eventIndex;
        public int loop;
        public float absoluteNormalized;
    }

    readonly List<Runtime> _active = new List<Runtime>(2);
    readonly List<PendingTrigger> _pending = new List<PendingTrigger>(8);

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int eventCount = events != null ? events.Count : 0;

        Runtime runtime = new Runtime(eventCount);
        runtime.lastNormalized = catchUpOnEnter ? 0f : stateInfo.normalizedTime;
        runtime.enterFrame = Time.frameCount;
        runtime.lastUpdateFrame = -1;

        _active.Add(runtime);

        for (int i = 0; i < eventCount; i++)
            events[i]?.OnStateEnter(animator, stateInfo, layerIndex, this);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int eventCount = events != null ? events.Count : 0;
        float currentNormalized = stateInfo.normalizedTime;
        int frame = Time.frameCount;

        Runtime runtime = SelectRuntime(currentNormalized, frame);
        if (runtime == null)
        {
            runtime = new Runtime(eventCount);
            runtime.lastNormalized = catchUpOnEnter ? 0f : currentNormalized;
            runtime.enterFrame = frame;
            _active.Add(runtime);
        }
        else
        {
            EnsureEventRuntimeSize(runtime, eventCount, true);
        }

        if (runtime.interrupted)
        {
            runtime.lastNormalized = currentNormalized;
            runtime.lastUpdateFrame = frame;
            return;
        }

        float previousNormalized = runtime.lastNormalized;

        if (resetIfTimeGoesBackwards && currentNormalized + backwardsEpsilon < previousNormalized)
        {
            ResetEventRuntime(runtime.ev);
            runtime.interrupted = false;
            previousNormalized = catchUpOnEnter ? 0f : currentNormalized;
        }

        ProcessRange(animator, stateInfo, layerIndex, runtime, previousNormalized, currentNormalized, eventCount);

        runtime.lastNormalized = currentNormalized;
        runtime.lastUpdateFrame = frame;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int eventCount = events != null ? events.Count : 0;
        Runtime runtime = SelectRuntime(stateInfo.normalizedTime, Time.frameCount, false);

        if (runtime != null && !runtime.interrupted && ShouldTreatExitAsInterrupt(stateInfo, runtime))
            ForceInterrupt(animator, stateInfo, layerIndex, runtime, eventCount);

        for (int i = 0; i < eventCount; i++)
            events[i]?.OnStateExit(animator, stateInfo, layerIndex, this);

        if (runtime != null)
            _active.Remove(runtime);
    }

    public void InterruptState(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        int eventCount = events != null ? events.Count : 0;
        Runtime runtime = SelectRuntime(stateInfo.normalizedTime, Time.frameCount, false);
        if (runtime == null)
            return;

        ForceInterrupt(animator, stateInfo, layerIndex, runtime, eventCount);
    }

    public static void InterruptBeforeTransition(Animator animator, int layerIndex = -1)
    {
        if (animator == null)
            return;

        if (layerIndex >= 0)
        {
            InterruptLayer(animator, layerIndex);
            return;
        }

        for (int i = 0; i < animator.layerCount; i++)
            InterruptLayer(animator, i);
    }

    static void InterruptLayer(Animator animator, int layerIndex)
    {
        InterruptStateInfo(animator, layerIndex, animator.GetCurrentAnimatorStateInfo(layerIndex));

        if (animator.IsInTransition(layerIndex))
            InterruptStateInfo(animator, layerIndex, animator.GetNextAnimatorStateInfo(layerIndex));
    }

    static void InterruptStateInfo(Animator animator, int layerIndex, AnimatorStateInfo stateInfo)
    {
        if (stateInfo.fullPathHash == 0)
            return;

        StateMachineBehaviour[] behaviours = animator.GetBehaviours(stateInfo.fullPathHash, layerIndex);
        if (behaviours == null)
            return;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is AnimatorStateEventManager manager)
                manager.InterruptState(animator, stateInfo, layerIndex);
        }
    }

    void ProcessRange(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex,
        Runtime runtime,
        float fromNormalized,
        float toNormalized,
        int eventCount)
    {
        if (events == null || eventCount == 0)
            return;

        if (toNormalized <= fromNormalized + SegmentEpsilon)
            return;

        EnsureEventRuntimeSize(runtime, eventCount, true);

        _pending.Clear();
        CollectPendingTriggers(animator, stateInfo, layerIndex, runtime, fromNormalized, toNormalized, eventCount, _pending);

        if (_pending.Count == 0)
            return;

        _pending.Sort(ComparePending);

        float processedTo = fromNormalized;

        for (int i = 0; i < _pending.Count; i++)
        {
            PendingTrigger pending = _pending[i];
            AnimatorStateEvent e = events[pending.eventIndex];

            if (e == null || !e.enabled)
                continue;

            ref EventRuntime er = ref runtime.ev[pending.eventIndex];

            if (e.frequency == AnimatorEventFrequency.OncePerState)
            {
                if (er.triggeredThisState)
                    continue;

                er.triggeredThisState = true;
            }
            else
            {
                if (er.lastTriggeredLoop == pending.loop)
                    continue;

                er.lastTriggeredLoop = pending.loop;
            }

            processedTo = pending.absoluteNormalized;
            e.OnTriggered(animator, stateInfo, layerIndex, false, this);

            if (runtime.interrupted)
                break;

            if (!StateStillActive(animator, stateInfo, layerIndex))
            {
                runtime.lastNormalized = processedTo;
                return;
            }
        }
    }

    void CollectPendingTriggers(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex,
        Runtime runtime,
        float fromNormalized,
        float toNormalized,
        int eventCount,
        List<PendingTrigger> pending)
    {
        int fromLoop = Mathf.FloorToInt(fromNormalized);
        float fromT = fromNormalized - fromLoop;

        int toLoop = Mathf.FloorToInt(toNormalized);
        float toT = toNormalized - toLoop;

        int perLoopStartLoop = fromLoop;
        float perLoopStartT = fromT;

        if (limitLoopCatchUp && maxLoopsPerUpdate > 0 && toLoop > fromLoop + maxLoopsPerUpdate)
        {
            perLoopStartLoop = toLoop - maxLoopsPerUpdate;
            perLoopStartT = 0f;
        }

        for (int i = 0; i < eventCount; i++)
        {
            AnimatorStateEvent e = events[i];
            if (e == null || !e.enabled)
                continue;

            float trigger = e.GetTriggerNormalized(animator, stateInfo, layerIndex);
            ref EventRuntime er = ref runtime.ev[i];

            if (e.frequency == AnimatorEventFrequency.OncePerState)
            {
                if (er.triggeredThisState)
                    continue;

                if (TryGetFirstCrossing(fromNormalized, toNormalized, trigger, out float absoluteCrossing, out int crossingLoop))
                {
                    pending.Add(new PendingTrigger
                    {
                        eventIndex = i,
                        loop = crossingLoop,
                        absoluteNormalized = absoluteCrossing
                    });
                }
            }
            else
            {
                for (int loop = perLoopStartLoop; loop <= toLoop; loop++)
                {
                    float segStart = loop == perLoopStartLoop ? perLoopStartT : 0f;
                    float segEnd = loop == toLoop ? toT : 1f;

                    if (!SegmentCrosses(segStart, segEnd, trigger))
                        continue;

                    if (er.lastTriggeredLoop == loop)
                        continue;

                    pending.Add(new PendingTrigger
                    {
                        eventIndex = i,
                        loop = loop,
                        absoluteNormalized = loop + trigger
                    });
                }
            }
        }
    }

    void ForceInterrupt(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex,
        Runtime runtime,
        int eventCount)
    {
        if (runtime.interrupted)
            return;

        runtime.interrupted = true;
        runtime.lastNormalized = stateInfo.normalizedTime;

        int loop = Mathf.FloorToInt(stateInfo.normalizedTime);

        for (int i = 0; i < eventCount; i++)
        {
            AnimatorStateEvent e = events[i];
            if (e == null || !e.enabled || !e.runOnInterrupt)
                continue;

            ref EventRuntime er = ref runtime.ev[i];

            if (e.frequency == AnimatorEventFrequency.OncePerState)
            {
                if (er.triggeredThisState)
                    continue;

                er.triggeredThisState = true;
            }
            else
            {
                if (er.lastTriggeredLoop == loop)
                    continue;

                er.lastTriggeredLoop = loop;
            }

            e.OnStateInterrupt(animator, stateInfo, layerIndex, this);
        }
    }

    Runtime SelectRuntime(float normalized, int frame, bool preferNotUpdatedThisFrame = true)
    {
        if (_active.Count == 0)
            return null;

        if (_active.Count == 1)
            return _active[0];

        Runtime best = null;
        float bestScore = float.PositiveInfinity;

        for (int pass = 0; pass < (preferNotUpdatedThisFrame ? 2 : 1); pass++)
        {
            best = null;
            bestScore = float.PositiveInfinity;

            for (int i = 0; i < _active.Count; i++)
            {
                Runtime runtime = _active[i];

                if (preferNotUpdatedThisFrame && pass == 0 && runtime.lastUpdateFrame == frame)
                    continue;

                float score = Mathf.Abs(normalized - runtime.lastNormalized);

                if (score < bestScore || (Mathf.Approximately(score, bestScore) && runtime.enterFrame > (best?.enterFrame ?? int.MinValue)))
                {
                    best = runtime;
                    bestScore = score;
                }
            }

            if (best != null)
                return best;
        }

        return _active[0];
    }

    bool ShouldTreatExitAsInterrupt(AnimatorStateInfo stateInfo, Runtime runtime)
    {
        if (!HasInterruptEvents())
            return false;

        if (stateInfo.loop)
            return true;

        float progress = Mathf.Max(runtime.lastNormalized, stateInfo.normalizedTime);
        return progress < CompletionThreshold;
    }

    bool HasInterruptEvents()
    {
        if (events == null)
            return false;

        for (int i = 0; i < events.Count; i++)
        {
            if (events[i] != null && events[i].enabled && events[i].runOnInterrupt)
                return true;
        }

        return false;
    }

    static bool StateStillActive(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (current.fullPathHash == stateInfo.fullPathHash)
            return true;

        if (animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layerIndex);
            if (next.fullPathHash == stateInfo.fullPathHash)
                return true;
        }

        return false;
    }

    static bool TryGetFirstCrossing(float fromNormalized, float toNormalized, float trigger, out float absoluteCrossing, out int loop)
    {
        absoluteCrossing = 0f;
        loop = 0;

        if (toNormalized <= fromNormalized + SegmentEpsilon)
            return false;

        int fromLoop = Mathf.FloorToInt(fromNormalized);
        float fromT = fromNormalized - fromLoop;

        int candidateLoop = fromT <= trigger ? fromLoop : fromLoop + 1;
        float candidate = candidateLoop + trigger;

        if (candidate > toNormalized + SegmentEpsilon)
            return false;

        absoluteCrossing = candidate;
        loop = candidateLoop;
        return true;
    }

    static bool SegmentCrosses(float start, float end, float trigger)
    {
        return start <= trigger && end >= trigger;
    }

    static int ComparePending(PendingTrigger a, PendingTrigger b)
    {
        int timeCompare = a.absoluteNormalized.CompareTo(b.absoluteNormalized);
        if (timeCompare != 0)
            return timeCompare;

        return a.eventIndex.CompareTo(b.eventIndex);
    }

    void EnsureEventRuntimeSize(Runtime runtime, int count, bool preserve)
    {
        if (runtime.ev != null && runtime.ev.Length == count)
            return;

        EventRuntime[] old = runtime.ev;
        runtime.ev = CreateEventRuntimeArray(count);

        if (!preserve || old == null)
            return;

        int copyCount = Mathf.Min(old.Length, runtime.ev.Length);
        for (int i = 0; i < copyCount; i++)
            runtime.ev[i] = old[i];
    }

    static EventRuntime[] CreateEventRuntimeArray(int count)
    {
        if (count <= 0)
            return Array.Empty<EventRuntime>();

        EventRuntime[] arr = new EventRuntime[count];

        for (int i = 0; i < arr.Length; i++)
            arr[i].lastTriggeredLoop = int.MinValue;

        return arr;
    }

    static void ResetEventRuntime(EventRuntime[] arr)
    {
        if (arr == null)
            return;

        for (int i = 0; i < arr.Length; i++)
        {
            arr[i].triggeredThisState = false;
            arr[i].lastTriggeredLoop = int.MinValue;
        }
    }
}