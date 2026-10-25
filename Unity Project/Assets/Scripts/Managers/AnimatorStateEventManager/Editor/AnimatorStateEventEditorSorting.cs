#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class AnimatorStateEventEditorSorting
{
    const float SecondsEpsilon = 0.0001f;

    sealed class SortItem
    {
        public AnimatorStateEvent evt;
        public int oldIndex;
        public int frameKey;
        public float secondsKey;
    }

    public static bool SortManager(AnimatorStateEventManager manager)
    {
        return SortManager(manager, null, out _);
    }

    public static bool SortManager(AnimatorStateEventManager manager, AnimatorStateEvent trackedEvent, out int trackedIndex)
    {
        trackedIndex = -1;

        if (manager == null || manager.events == null || manager.events.Count <= 1)
        {
            if (manager != null && manager.events != null && trackedEvent != null)
                trackedIndex = manager.events.IndexOf(trackedEvent);
            return false;
        }

        AnimatorStateEventEditorClipResolver.TryGetOwningClip(manager, out AnimationClip clip, out _);

        List<SortItem> items = new List<SortItem>(manager.events.Count);
        for (int i = 0; i < manager.events.Count; i++)
        {
            AnimatorStateEvent evt = manager.events[i];
            items.Add(new SortItem
            {
                evt = evt,
                oldIndex = i,
                frameKey = GetFrameKey(evt, clip),
                secondsKey = GetSecondsKey(evt, clip)
            });
        }

        bool hasClip = clip != null && clip.frameRate > 0f && clip.length > 0f;

        items.Sort((a, b) =>
        {
            if (ReferenceEquals(a.evt, b.evt))
                return 0;

            if (a.evt == null) return 1;
            if (b.evt == null) return -1;

            if (hasClip)
            {
                int c = a.frameKey.CompareTo(b.frameKey);
                if (c != 0)
                    return c;
            }
            else
            {
                int c = CompareSeconds(a.secondsKey, b.secondsKey);
                if (c != 0)
                    return c;
            }

            return a.oldIndex.CompareTo(b.oldIndex);
        });

        bool changed = false;
        for (int i = 0; i < items.Count; i++)
        {
            if (!ReferenceEquals(manager.events[i], items[i].evt))
            {
                changed = true;
                break;
            }
        }

        if (!changed)
        {
            if (trackedEvent != null)
                trackedIndex = manager.events.IndexOf(trackedEvent);
            return false;
        }

        manager.events.Clear();
        for (int i = 0; i < items.Count; i++)
            manager.events.Add(items[i].evt);

        if (trackedEvent != null)
            trackedIndex = manager.events.IndexOf(trackedEvent);

        return true;
    }

    public static bool SortSerializedTargets(SerializedObject serializedObject)
    {
        if (serializedObject == null || serializedObject.targetObjects == null)
            return false;

        bool changed = false;

        for (int i = 0; i < serializedObject.targetObjects.Length; i++)
        {
            if (serializedObject.targetObjects[i] is AnimatorStateEventManager manager)
            {
                if (SortManager(manager))
                {
                    EditorUtility.SetDirty(manager);
                    changed = true;
                }
            }
        }

        return changed;
    }

    static int CompareSeconds(float a, float b)
    {
        if (Mathf.Abs(a - b) <= SecondsEpsilon)
            return 0;

        return a < b ? -1 : 1;
    }

    static float GetSecondsKey(AnimatorStateEvent evt, AnimationClip clip)
    {
        if (evt == null)
            return float.PositiveInfinity;

        if (clip == null || clip.length <= 0f)
        {
            switch (evt.timeMode)
            {
                case AnimatorEventTimeMode.Seconds:
                    return Mathf.Max(0f, evt.seconds);

                case AnimatorEventTimeMode.ClipFrame:
                    return Mathf.Max(0, evt.frame);

                default:
                    return Mathf.Clamp01(evt.normalizedTime);
            }
        }

        switch (evt.timeMode)
        {
            case AnimatorEventTimeMode.Seconds:
                return Mathf.Clamp(evt.seconds, 0f, clip.length);

            case AnimatorEventTimeMode.ClipFrame:
                return FrameToSeconds(Mathf.Clamp(evt.frame, 0, GetMaxFrame(clip)), clip);

            default:
                return Mathf.Clamp01(evt.normalizedTime) * clip.length;
        }
    }

    static int GetFrameKey(AnimatorStateEvent evt, AnimationClip clip)
    {
        if (evt == null)
            return int.MaxValue;

        if (clip == null || clip.frameRate <= 0f || clip.length <= 0f)
            return 0;

        int maxFrame = GetMaxFrame(clip);

        switch (evt.timeMode)
        {
            case AnimatorEventTimeMode.ClipFrame:
                return Mathf.Clamp(evt.frame, 0, maxFrame);

            case AnimatorEventTimeMode.Seconds:
                return SecondsToFrame(evt.seconds, clip, maxFrame);

            default:
                return SecondsToFrame(Mathf.Clamp01(evt.normalizedTime) * clip.length, clip, maxFrame);
        }
    }

    static int GetMaxFrame(AnimationClip clip)
    {
        return AnimatorStateEventEditorClipResolver.GetMaxFrame(clip);
    }

    static int SecondsToFrame(float seconds, AnimationClip clip, int maxFrame)
    {
        if (clip == null || clip.frameRate <= 0f)
            return 0;

        int frame = Mathf.RoundToInt(Mathf.Max(0f, seconds) * clip.frameRate);
        return Mathf.Clamp(frame, 0, maxFrame);
    }

    static float FrameToSeconds(int frame, AnimationClip clip)
    {
        if (clip == null || clip.frameRate <= 0f)
            return 0f;

        return Mathf.Max(0, frame) / clip.frameRate;
    }
}
#endif