#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(AnimatorStateEventManager))]
public sealed class AnimatorStateEventManagerEditor : Editor
{
    [Serializable]
    struct ManagerClipboardData
    {
        public bool catchUpOnEnter;
        public bool resetIfTimeGoesBackwards;
        public float backwardsEpsilon;
        public bool limitLoopCatchUp;
        public int maxLoopsPerUpdate;
        public List<AnimatorStateEvent> events;
    }

    static bool s_hasManagerClipboard;
    static ManagerClipboardData s_managerClipboard;

    SerializedProperty _statePath;
    SerializedProperty _catchUpOnEnter;
    SerializedProperty _resetIfTimeGoesBackwards;
    SerializedProperty _backwardsEpsilon;
    SerializedProperty _limitLoopCatchUp;
    SerializedProperty _maxLoopsPerUpdate;
    SerializedProperty _events;

    ReorderableList _list;
    List<Type> _types = new List<Type>();
    List<string> _paths = new List<string>();
    readonly HashSet<string> _missingEventWarningKeys = new HashSet<string>();
    const float ElementLeftInset = 24f;

    bool _ready;

    void OnEnable()
    {
        _ready = false;
        _missingEventWarningKeys.Clear();

        if (target == null || targets == null || targets.Length == 0 || targets[0] == null)
            return;

        _statePath = serializedObject.FindProperty("statePath");
        _catchUpOnEnter = serializedObject.FindProperty("catchUpOnEnter");
        _resetIfTimeGoesBackwards = serializedObject.FindProperty("resetIfTimeGoesBackwards");
        _backwardsEpsilon = serializedObject.FindProperty("backwardsEpsilon");
        _limitLoopCatchUp = serializedObject.FindProperty("limitLoopCatchUp");
        _maxLoopsPerUpdate = serializedObject.FindProperty("maxLoopsPerUpdate");
        _events = serializedObject.FindProperty("events");

        if (_events == null)
            return;

        AnimatorStateEventEditorClipResolver.Invalidate(target);
        UpdateStoredStatePath();

        BuildTypeCache();
        BuildList();

        _ready = true;
    }

    public override void OnInspectorGUI()
    {
        if (target == null)
            return;

        if (!_ready || _events == null || _list == null)
        {
            OnEnable();
            if (!_ready) return;
        }

        serializedObject.UpdateIfRequiredOrScript();
        UpdateStoredStatePath();

        DrawSettings();
        EditorGUILayout.Space(4f);
        DrawClipboardPanel();
        EditorGUILayout.Space(4f);
        DrawClipPanel();
        EditorGUILayout.Space(4f);

        AnimatorStateEvent selectedBefore = GetSelectedEventReference();

        EditorGUI.BeginChangeCheck();
        _list.DoLayoutList();
        bool listChanged = EditorGUI.EndChangeCheck();

        bool applied = serializedObject.ApplyModifiedProperties();

        if (listChanged || applied)
        {
            SortAndRestoreSelection(selectedBefore);
            Repaint();
        }
    }

    void DrawSettings()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Manager Settings", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Path", _statePath != null ? _statePath.stringValue : "Unknown State");
            EditorGUILayout.PropertyField(_catchUpOnEnter);
            EditorGUILayout.PropertyField(_resetIfTimeGoesBackwards);

            using (new EditorGUI.DisabledScope(_resetIfTimeGoesBackwards != null && !_resetIfTimeGoesBackwards.boolValue))
                EditorGUILayout.PropertyField(_backwardsEpsilon);

            EditorGUILayout.PropertyField(_limitLoopCatchUp);

            using (new EditorGUI.DisabledScope(_limitLoopCatchUp != null && !_limitLoopCatchUp.boolValue))
                EditorGUILayout.PropertyField(_maxLoopsPerUpdate);
        }
    }

    void DrawClipboardPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Copy / Paste", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Manager"))
                    CopyManagerClipboard();

                using (new EditorGUI.DisabledScope(!s_hasManagerClipboard))
                {
                    if (GUILayout.Button("Paste Manager"))
                        PasteManagerClipboard();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Events"))
                    CopyEventsClipboard();

                using (new EditorGUI.DisabledScope(!AnimatorStateEventEditorClipboard.HasEventList))
                {
                    if (GUILayout.Button("Paste Events"))
                        PasteEventsClipboard(append: false);

                    if (GUILayout.Button("Append Events"))
                        PasteEventsClipboard(append: true);
                }
            }
        }
    }

    void DrawClipPanel()
    {
        bool hasClip = AnimatorStateEventEditorClipResolver.TryGetOwningClip(target, out var clip, out var statePath);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("State Clip", EditorStyles.boldLabel);

            if (hasClip && clip != null)
            {
                EditorGUILayout.LabelField($"State: {statePath}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Clip: {clip.name} | {clip.length:0.###}s @ {clip.frameRate:0.##}fps | max frame {AnimatorStateEventEditorClipResolver.GetMaxFrame(clip)}",
                    EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("No clip resolved for this state (or state uses no clip motion).", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Clip Binding"))
                {
                    AnimatorStateEventEditorClipResolver.Invalidate(target);
                    Repaint();
                    return;
                }

                using (new EditorGUI.DisabledScope(!hasClip || clip == null))
                {
                    if (GUILayout.Button("Select Clip"))
                        Selection.activeObject = clip;

                    if (GUILayout.Button("Open Event Timeline"))
                    {
                        AnimatorStateEventTimelineLauncher.Open((AnimatorStateEventManager)target);
                    }
                }
            }
        }
    }

    AnimatorStateEvent GetSelectedEventReference()
    {
        var mgr = target as AnimatorStateEventManager;
        if (mgr == null || mgr.events == null)
            return null;

        if (_list == null || _list.index < 0 || _list.index >= mgr.events.Count)
            return null;

        return mgr.events[_list.index];
    }

    void SortAndRestoreSelection(AnimatorStateEvent tracked)
    {
        var mgr = target as AnimatorStateEventManager;
        if (mgr == null)
            return;

        int newIndex;
        bool changed = AnimatorStateEventEditorSorting.SortManager(mgr, tracked, out newIndex);

        if (changed)
        {
            EditorUtility.SetDirty(mgr);
            serializedObject.UpdateIfRequiredOrScript();
        }

        if (mgr.events == null || mgr.events.Count == 0)
        {
            _list.index = -1;
            return;
        }

        if (tracked != null && newIndex >= 0)
            _list.index = newIndex;
        else if (_list.index >= mgr.events.Count)
            _list.index = mgr.events.Count - 1;
    }

    void UpdateStoredStatePath()
    {
        if (_statePath == null)
            return;

        string resolved = "Unknown State";

        if (AnimatorStateEventEditorClipResolver.TryGetOwningClip(target, out _, out var resolvedStatePath) &&
            !string.IsNullOrWhiteSpace(resolvedStatePath))
        {
            resolved = resolvedStatePath;
        }

        if (_statePath.stringValue != resolved)
            _statePath.stringValue = resolved;
    }

    void BuildList()
    {
        _list = new ReorderableList(serializedObject, _events, true, true, true, true);

        _list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Animator State Events");
        };

        _list.elementHeightCallback = index =>
        {
            if (_events == null || index < 0 || index >= _events.arraySize)
                return EditorGUIUtility.singleLineHeight + 6f;

            var element = _events.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true) + 6f;
        };

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            if (_events == null || index < 0 || index >= _events.arraySize)
                return;

            rect.y += 2f;
            rect.height -= 4f;

            var element = _events.GetArrayElementAtIndex(index);
            string title = $"#{index + 1} {GetTypeName(element)}";
            EditorGUI.PropertyField(rect, element, new GUIContent(title), true);
        };

        _list.onAddDropdownCallback = (rect, list) =>
        {
            var menu = new GenericMenu();

            if (_types == null || _types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No AnimatorStateEvent types found"));
            }
            else
            {
                for (int i = 0; i < _types.Count; i++)
                {
                    int capture = i;
                    menu.AddItem(new GUIContent(_paths[capture]), false, () => AddEvent(_types[capture]));
                }
            }

            menu.DropDown(rect);
        };

        _list.onRemoveCallback = list =>
        {
            if (_events == null || list.index < 0 || list.index >= _events.arraySize)
                return;

            Undo.RecordObject(target, "Remove Animator State Event");

            serializedObject.UpdateIfRequiredOrScript();
            _events.DeleteArrayElementAtIndex(list.index);
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(target);
            Repaint();
        };

        _list.onReorderCallback = list =>
        {
            EditorUtility.SetDirty(target);
            Repaint();
        };
    }

    void AddEvent(Type t)
    {
        if (_events == null || t == null || !typeof(AnimatorStateEvent).IsAssignableFrom(t))
            return;

        Undo.RecordObject(target, "Add Animator State Event");

        serializedObject.UpdateIfRequiredOrScript();

        _events.arraySize++;
        var element = _events.GetArrayElementAtIndex(_events.arraySize - 1);

        var inst = (AnimatorStateEvent)Activator.CreateInstance(t);
        inst.timeMode = AnimatorEventTimeMode.ClipFrame;
        inst.normalizedTime = 0f;
        inst.seconds = 0f;
        inst.frame = 0;

        element.managedReferenceValue = inst;
        element.isExpanded = true;

        serializedObject.ApplyModifiedProperties();

        SortAndRestoreSelection(inst);

        EditorUtility.SetDirty(target);
        Repaint();
    }

    void CopyEventsClipboard()
    {
        var mgr = target as AnimatorStateEventManager;
        if (mgr == null)
            return;

        AnimatorStateEventEditorClipboard.CopyEventList(mgr.events ?? new List<AnimatorStateEvent>());
    }

    void PasteEventsClipboard(bool append)
    {
        if (!AnimatorStateEventEditorClipboard.TryGetEventListClone(out var eventsToPaste))
            return;

        Undo.RecordObject(target, append ? "Append Animator State Events" : "Paste Animator State Events");

        serializedObject.UpdateIfRequiredOrScript();

        int start = append ? _events.arraySize : 0;

        if (!append)
            _events.arraySize = 0;

        _events.arraySize = start + eventsToPaste.Count;

        AnimatorStateEvent tracked = null;

        for (int i = 0; i < eventsToPaste.Count; i++)
        {
            var el = _events.GetArrayElementAtIndex(start + i);
            el.managedReferenceValue = eventsToPaste[i];
            el.isExpanded = true;

            if (i == eventsToPaste.Count - 1)
                tracked = eventsToPaste[i];
        }

        serializedObject.ApplyModifiedProperties();

        SortAndRestoreSelection(tracked);

        EditorUtility.SetDirty(target);
        Repaint();
    }

    void CopyManagerClipboard()
    {
        serializedObject.UpdateIfRequiredOrScript();

        s_managerClipboard = new ManagerClipboardData
        {
            catchUpOnEnter = _catchUpOnEnter != null && _catchUpOnEnter.boolValue,
            resetIfTimeGoesBackwards = _resetIfTimeGoesBackwards != null && _resetIfTimeGoesBackwards.boolValue,
            backwardsEpsilon = _backwardsEpsilon != null ? _backwardsEpsilon.floatValue : 0f,
            limitLoopCatchUp = _limitLoopCatchUp != null && _limitLoopCatchUp.boolValue,
            maxLoopsPerUpdate = _maxLoopsPerUpdate != null ? _maxLoopsPerUpdate.intValue : 0,
            events = ReadEventsFromSerializedProperty(_events)
        };

        s_hasManagerClipboard = true;
    }

    void PasteManagerClipboard()
    {
        if (!s_hasManagerClipboard)
            return;

        Undo.RecordObject(target, "Paste Animator State Event Manager");

        serializedObject.UpdateIfRequiredOrScript();

        if (_catchUpOnEnter != null) _catchUpOnEnter.boolValue = s_managerClipboard.catchUpOnEnter;
        if (_resetIfTimeGoesBackwards != null) _resetIfTimeGoesBackwards.boolValue = s_managerClipboard.resetIfTimeGoesBackwards;
        if (_backwardsEpsilon != null) _backwardsEpsilon.floatValue = s_managerClipboard.backwardsEpsilon;
        if (_limitLoopCatchUp != null) _limitLoopCatchUp.boolValue = s_managerClipboard.limitLoopCatchUp;
        if (_maxLoopsPerUpdate != null) _maxLoopsPerUpdate.intValue = s_managerClipboard.maxLoopsPerUpdate;

        WriteEventsToSerializedProperty(_events, s_managerClipboard.events, append: false);

        serializedObject.ApplyModifiedProperties();

        SortAndRestoreSelection(null);

        EditorUtility.SetDirty(target);
        Repaint();
    }

    static List<AnimatorStateEvent> ReadEventsFromSerializedProperty(SerializedProperty eventsProp)
    {
        if (eventsProp == null || !eventsProp.isArray)
            return new List<AnimatorStateEvent>();

        var list = new List<AnimatorStateEvent>(eventsProp.arraySize);
        for (int i = 0; i < eventsProp.arraySize; i++)
        {
            var element = eventsProp.GetArrayElementAtIndex(i);
            list.Add(AnimatorStateEventEditorClipboard.CloneEvent(element.managedReferenceValue as AnimatorStateEvent));
        }

        return list;
    }

    static void WriteEventsToSerializedProperty(SerializedProperty eventsProp, IList<AnimatorStateEvent> src, bool append)
    {
        if (eventsProp == null || !eventsProp.isArray)
            return;

        int start = append ? eventsProp.arraySize : 0;
        if (!append)
            eventsProp.arraySize = 0;

        int count = src != null ? src.Count : 0;
        eventsProp.arraySize = start + count;

        for (int i = 0; i < count; i++)
        {
            var element = eventsProp.GetArrayElementAtIndex(start + i);
            element.managedReferenceValue = AnimatorStateEventEditorClipboard.CloneEvent(src[i]);
            element.isExpanded = true;
        }
    }

    void BuildTypeCache()
    {
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => t != null && !t.IsAbstract && typeof(AnimatorStateEvent).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();

        _paths = new List<string>(_types.Count);

        foreach (var t in _types)
        {
            var attr = (AnimatorStateEventMenuAttribute)Attribute.GetCustomAttribute(t, typeof(AnimatorStateEventMenuAttribute));
            string path = (attr != null && !string.IsNullOrWhiteSpace(attr.path))
                ? attr.path
                : ObjectNames.NicifyVariableName(t.Name);

            _paths.Add(path);
        }
    }

    string GetResolvedStatePathForLog()
    {
        if (target != null &&
            AnimatorStateEventEditorClipResolver.TryGetOwningClip(target, out _, out var statePath) &&
            !string.IsNullOrWhiteSpace(statePath))
        {
            return statePath;
        }

        return "Unknown State Path";
    }

    string GetTypeName(SerializedProperty element)
    {
        object obj = element != null ? element.managedReferenceValue : null;

        if (obj != null)
            return obj.GetType().Name;

        return element != null
            ? $"Missing Event ({element.propertyPath})"
            : "Missing Event";
    }
}
#endif