#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimatorStateEventEditorClipResolver
{
    struct CacheEntry
    {
        public AnimationClip clip;
        public string statePath;
    }

    static readonly Dictionary<int, CacheEntry> _cache = new Dictionary<int, CacheEntry>();

    public static void Invalidate(Object behaviourObj)
    {
        if (behaviourObj == null) return;
        _cache.Remove(behaviourObj.GetInstanceID());
    }

    public static void ClearCache()
    {
        _cache.Clear();
    }

    public static bool TryGetOwningClip(Object behaviourObj, out AnimationClip clip, out string statePath)
    {
        clip = null;
        statePath = null;

        if (behaviourObj == null) return false;

        int id = behaviourObj.GetInstanceID();
        if (_cache.TryGetValue(id, out var ce) && ce.clip != null)
        {
            clip = ce.clip;
            statePath = ce.statePath;
            return true;
        }

        string assetPath = AssetDatabase.GetAssetPath(behaviourObj);
        if (string.IsNullOrEmpty(assetPath)) return false;

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
        if (controller == null) return false;

        for (int l = 0; l < controller.layers.Length; l++)
        {
            var layer = controller.layers[l];
            if (layer.stateMachine == null) continue;

            if (FindStateRecursive(layer.stateMachine, layer.name, behaviourObj, out var st, out var foundPath))
            {
                var foundClip = GetRepresentativeClip(st.motion);
                if (foundClip == null) return false;

                ce = new CacheEntry { clip = foundClip, statePath = foundPath };
                _cache[id] = ce;

                clip = foundClip;
                statePath = foundPath;
                return true;
            }
        }

        return false;
    }

    public static int GetMaxFrame(AnimationClip clip)
    {
        if (clip == null || clip.length <= 0f || clip.frameRate <= 0f) return 0;
        int total = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
        return Mathf.Max(0, total - 1);
    }

    static bool FindStateRecursive(AnimatorStateMachine sm, string prefix, Object behaviourObj, out AnimatorState found, out string path)
    {
        foreach (var child in sm.states)
        {
            var st = child.state;
            if (st == null) continue;

            var behaviours = st.behaviours;
            if (behaviours != null)
            {
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if ((Object)behaviours[i] == behaviourObj)
                    {
                        found = st;
                        path = $"{prefix}/{st.name}";
                        return true;
                    }
                }
            }
        }

        foreach (var sub in sm.stateMachines)
        {
            var subSm = sub.stateMachine;
            if (subSm == null) continue;

            if (FindStateRecursive(subSm, $"{prefix}/{subSm.name}", behaviourObj, out found, out path))
                return true;
        }

        found = null;
        path = null;
        return false;
    }

    static AnimationClip GetRepresentativeClip(Motion motion)
    {
        if (motion == null) return null;

        if (motion is AnimationClip clip)
            return clip;

        if (motion is BlendTree bt)
        {
            var clips = new List<AnimationClip>(8);
            CollectClips(bt, clips);
            if (clips.Count == 0) return null;

            AnimationClip best = clips[0];
            float bestLen = best != null ? best.length : 0f;

            for (int i = 1; i < clips.Count; i++)
            {
                var c = clips[i];
                if (c == null) continue;
                if (c.length > bestLen)
                {
                    best = c;
                    bestLen = c.length;
                }
            }

            return best;
        }

        return null;
    }

    static void CollectClips(BlendTree bt, List<AnimationClip> outClips)
    {
        if (bt == null) return;

        foreach (var child in bt.children)
        {
            if (child.motion is AnimationClip c) outClips.Add(c);
            else if (child.motion is BlendTree sub) CollectClips(sub, outClips);
        }
    }
}
#endif