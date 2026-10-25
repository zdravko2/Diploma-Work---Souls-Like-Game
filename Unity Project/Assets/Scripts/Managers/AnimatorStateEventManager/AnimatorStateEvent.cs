using System;
using UnityEngine;

public enum AnimatorEventTimeMode { Normalized, Seconds, ClipFrame }
public enum AnimatorEventFrequency { OncePerState, OncePerLoop }

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AnimatorStateEventMenuAttribute : Attribute
{
    public readonly string path;
    public AnimatorStateEventMenuAttribute(string path) => this.path = path;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AnimatorStateEventColorAttribute : Attribute
{
    public readonly float r;
    public readonly float g;
    public readonly float b;
    public readonly float a;
    public readonly string html;

    public AnimatorStateEventColorAttribute(float r, float g, float b) : this(r, g, b, 1f) { }

    public AnimatorStateEventColorAttribute(float r, float g, float b, float a)
    {
        this.r = r;
        this.g = g;
        this.b = b;
        this.a = a;
        this.html = null;
    }

    public AnimatorStateEventColorAttribute(string html)
    {
        this.r = 0f;
        this.g = 0f;
        this.b = 0f;
        this.a = 1f;
        this.html = html;
    }
}

[Serializable]
public abstract class AnimatorStateEvent
{
    public bool enabled = true;

    public AnimatorEventFrequency frequency = AnimatorEventFrequency.OncePerState;
    public AnimatorEventTimeMode timeMode = AnimatorEventTimeMode.ClipFrame;
    [Range(0f, 1f)] public float normalizedTime = 0f;
    [Min(0f)] public float seconds = 0f;
    [Min(0)] public int frame = 0;

    public bool runOnInterrupt = false;

    public virtual void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager) { }
    public abstract void OnTriggered(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, bool forced, AnimatorStateEventManager manager);
    public virtual void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager) { }

    public virtual void OnStateInterrupt(Animator animator, AnimatorStateInfo stateInfo, int layerIndex, AnimatorStateEventManager manager)
    {
        OnTriggered(animator, stateInfo, layerIndex, true, manager);
    }

    public float GetTriggerNormalized(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        switch (timeMode)
        {
            case AnimatorEventTimeMode.Seconds:
                {
                    float len = Mathf.Max(0.0001f, stateInfo.length);
                    return Mathf.Clamp01(seconds / len);
                }

            case AnimatorEventTimeMode.ClipFrame:
                {
                    AnimationClip clip = AnimatorStateEventUtilities.GetClipForState(animator, stateInfo, layerIndex);
                    if (clip == null || clip.frameRate <= 0f || clip.length <= 0f)
                        return Mathf.Clamp01(normalizedTime);

                    float totalFrames = clip.length * clip.frameRate;
                    float denom = Mathf.Max(1f, totalFrames - 1f);

                    float f = Mathf.Clamp(frame, 0, Mathf.RoundToInt(totalFrames - 1f));
                    return Mathf.Clamp01(f / denom);
                }

            default:
                return Mathf.Clamp01(normalizedTime);
        }
    }
}

public static class AnimatorStateEventUtilities
{
    public static AnimationClip GetClipForState(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null) return null;

        AnimatorClipInfo[] infos;

        if (animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
            bool thisIsNext = nextState.fullPathHash == stateInfo.fullPathHash;

            infos = thisIsNext
                ? animator.GetNextAnimatorClipInfo(layerIndex)
                : animator.GetCurrentAnimatorClipInfo(layerIndex);
        }
        else
        {
            infos = animator.GetCurrentAnimatorClipInfo(layerIndex);
        }

        if (infos == null || infos.Length == 0) return null;

        int best = 0;
        float bestWeight = infos[0].weight;

        for (int i = 1; i < infos.Length; i++)
        {
            if (infos[i].weight > bestWeight)
            {
                bestWeight = infos[i].weight;
                best = i;
            }
        }

        return infos[best].clip;
    }
}