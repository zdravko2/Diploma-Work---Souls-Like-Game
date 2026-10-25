#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class AnimatorStateEventTimelineWindow : EditorWindow
{
    const string GlobalPrefsPrefix = "AnimatorStateEventTimelineWindow";

    const float ToolbarHeight = 22f;
    const float RulerHeight = 28f;
    const float LaneHeaderWidth = 170f;

    const float DefaultLaneRowHeight = 22f;
    const float DefaultTimelineMinHeight = 180f;

    const float MinZoom = 40f;
    const float MaxZoom = 1200f;

    const float MarkerWidth = 8f;
    const float MarkerRowPadding = 2f;

    const float DragStartThresholdPx = 3f;

    const float EventPreviewMinHeight = 90f;
    const float EventPreviewMaxHeight = 420f;
    const float EventPreviewSplitterHeight = 8f;

    const float EventPreviewPad = 2f;
    const float EventPreviewBtnW = 18f;
    const float EventPreviewToggleW = 18f;
    const float EventPreviewToggleGap = 8f;
    const float EventPreviewHeaderAccentW = 3f;
    const float CategoryOrderButtonWidth = 18f;
    const float CategoryOrderButtonsGap = 2f;
    int _eventDragUndoGroup = -1;

    Vector2 _selectedEventPreviewScroll;

    bool _isResizingEventPreview;
    float _eventPreviewResizeStartMouseY;
    float _eventPreviewResizeStartHeight;

    [Serializable]
    sealed class PersistedState
    {
        public bool displayFrames = false;
        public bool snapToFrames = true;
        public float zoomPxPerSecond = 140f;
        public float scrollX = 0f;
        public float scrollY = 0f;
        public float playheadSeconds = 0f;

        public float eventPreviewHeight = 170f;
    }

    [Serializable]
    sealed class MarkerLayoutInfo
    {
        public int eventIndex;
        public float rawSeconds;
        public float snappedSeconds;
        public int frame;
        public int stackRow;
    }

    [Serializable]
    sealed class CategoryInfo
    {
        public string key;
        public string display;
        public Color color;
        public readonly List<int> eventIndices = new List<int>();
        public readonly List<MarkerLayoutInfo> markers = new List<MarkerLayoutInfo>();
        public int stackRowCount = 1;
    }

    sealed class MarkerHitInfo
    {
        public int eventIndex;
        public string categoryKey;
        public float snappedSeconds;
        public Rect rect;
    }

    AnimatorStateEventManager _manager;
    AnimationClip _clip;
    string _statePath;

    PersistedState _state = new PersistedState();

    Vector2 _scroll;
    float _laneRowHeight = DefaultLaneRowHeight;
    bool _pendingSave;

    bool _pendingMarkerDrag;
    bool _pendingPlayheadDrag;
    Vector2 _pointerDownMousePos;
    MarkerHitInfo _pendingMarkerHit;
    float _pendingPlayheadClipLength;
    Rect _pendingPlayheadTimelineRect;

    bool _isDraggingPlayhead;
    bool _isDraggingEvent;
    int _draggingEventIndex = -1;
    float _draggingEventMouseOffsetSeconds;

    bool _hasDeferredMarkerHover;
    Rect _deferredMarkerHoverLaneRect;
    Rect _deferredMarkerHoverMarkerRect;
    string _deferredMarkerHoverText;

    int _selectedEventIndex = -1;

    readonly List<CategoryInfo> _categories = new List<CategoryInfo>();
    readonly List<MarkerHitInfo> _markerHits = new List<MarkerHitInfo>();

    static string GlobalPrefsKey => GlobalPrefsPrefix + ".Global";

    public static void Open(AnimatorStateEventManager manager)
    {
        var window = GetWindow<AnimatorStateEventTimelineWindow>("Animator Event Timeline");
        window.minSize = new Vector2(520f, 420f);
        window.SetManager(manager);
        window.Show();
        window.Focus();
    }

    [MenuItem("Window/Animation/Animator Event Timeline")]
    static void OpenEmpty()
    {
        var window = GetWindow<AnimatorStateEventTimelineWindow>("Animator Event Timeline");
        window.minSize = new Vector2(520f, 420f);
        window.Show();
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        EditorApplication.projectChanged += OnProjectChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        SaveStateNow();
    }

    void OnUndoRedoPerformed()
    {
        RebuildCategories();

        if (_manager != null && _manager.events != null)
        {
            if (_selectedEventIndex >= _manager.events.Count)
                _selectedEventIndex = _manager.events.Count - 1;
            if (_selectedEventIndex < 0 && _manager.events.Count > 0)
                _selectedEventIndex = Mathf.Min(_selectedEventIndex, _manager.events.Count - 1);
        }
        else
        {
            _selectedEventIndex = -1;
        }

        Repaint();
    }

    void OnSelectionChange()
    {
        if (_manager != null)
            Repaint();
    }

    void OnProjectChanged()
    {
        if (_manager == null)
            return;

        RefreshClipBinding();
        RebuildCategories();
        Repaint();
    }

    void OnPlayModeChanged(PlayModeStateChange _)
    {
        Repaint();
    }

    void Update()
    {
        if (_pendingSave)
            SaveStateNow();
    }

    public void SetManager(AnimatorStateEventManager manager)
    {
        _manager = manager;
        _selectedEventIndex = -1;
        EndPointerDrag();
        RefreshClipBinding();
        LoadState();
        RebuildCategories();
        Repaint();
    }

    void RefreshClipBinding()
    {
        _clip = null;
        _statePath = null;

        if (_manager == null)
            return;

        AnimatorStateEventEditorClipResolver.Invalidate(_manager);
        AnimatorStateEventEditorClipResolver.TryGetOwningClip(_manager, out _clip, out _statePath);
    }

    void LoadState()
    {
        _state = new PersistedState();

        if (EditorPrefs.HasKey(GlobalPrefsKey))
        {
            try
            {
                string json = EditorPrefs.GetString(GlobalPrefsKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = JsonUtility.FromJson<PersistedState>(json);
                    if (loaded != null)
                        _state = loaded;
                }
            }
            catch
            {
                _state = new PersistedState();
            }
        }

        SanitizePersistedStateValues();

        _state.zoomPxPerSecond = Mathf.Clamp(_state.zoomPxPerSecond, MinZoom, MaxZoom);
        _state.playheadSeconds = Mathf.Max(0f, _state.playheadSeconds);
        _state.eventPreviewHeight = Mathf.Clamp(_state.eventPreviewHeight, EventPreviewMinHeight, EventPreviewMaxHeight);

        _scroll = new Vector2(Mathf.Max(0f, _state.scrollX), Mathf.Max(0f, _state.scrollY));
    }

    void SaveStateNow()
    {
        _pendingSave = false;

        _state.scrollX = Mathf.Max(0f, FiniteOr(_scroll.x, 0f));
        _state.scrollY = Mathf.Max(0f, FiniteOr(_scroll.y, 0f));

        SanitizePersistedStateValues();

        _state.zoomPxPerSecond = Mathf.Clamp(_state.zoomPxPerSecond, MinZoom, MaxZoom);
        _state.playheadSeconds = Mathf.Max(0f, _state.playheadSeconds);
        _state.eventPreviewHeight = Mathf.Clamp(_state.eventPreviewHeight, EventPreviewMinHeight, EventPreviewMaxHeight);

        EditorPrefs.SetString(GlobalPrefsKey, JsonUtility.ToJson(_state));
    }

    void SanitizePersistedStateValues()
    {
        if (_state == null)
            _state = new PersistedState();

        _state.zoomPxPerSecond = FiniteOr(_state.zoomPxPerSecond, 140f);
        _state.scrollX = Mathf.Max(0f, FiniteOr(_state.scrollX, 0f));
        _state.scrollY = Mathf.Max(0f, FiniteOr(_state.scrollY, 0f));
        _state.playheadSeconds = Mathf.Max(0f, FiniteOr(_state.playheadSeconds, 0f));
        _state.eventPreviewHeight = FiniteOr(_state.eventPreviewHeight, 170f);
    }

    void SaveStateSoon()
    {
        _pendingSave = true;
    }

    void OnGUI()
    {
        DrawTopToolbar();
        DrawStatePathBanner();

        if (_manager == null)
        {
            DrawNoManagerUI();
            return;
        }

        if (_clip == null)
        {
            DrawMissingClipUI();
            return;
        }

        HandleGlobalShortcuts();

        DrawSelectedEventPreviewPane();
        DrawEventPreviewResizeHandle();
        DrawTimelineArea();
    }

    void HandleGlobalShortcuts()
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown)
            return;

        bool action = e.control || e.command;
        if (!action)
            return;

        // Let text fields keep native shortcuts.
        if (EditorGUIUtility.editingTextField)
            return;

        // Copy selected
        if (e.keyCode == KeyCode.C)
        {
            if (HasValidSelectedEvent())
            {
                CopyEventAtIndex(_selectedEventIndex);
                e.Use();
            }
            return;
        }

        // Paste:
        // - Ctrl/Cmd+V => insert after selected (or at playhead if none selected)
        // - Ctrl/Cmd+Shift+V => replace selected (if selected), otherwise paste at playhead
        if (e.keyCode == KeyCode.V)
        {
            if (!AnimatorStateEventEditorClipboard.HasEvent)
                return;

            if (e.shift)
            {
                if (HasValidSelectedEvent())
                    PasteEventReplaceAtIndex(_selectedEventIndex);
                else
                    PasteClipboardEventAtTime(_state.playheadSeconds, -1);

                e.Use();
                return;
            }

            if (HasValidSelectedEvent())
                PasteEventAfterIndex(_selectedEventIndex);
            else
                PasteClipboardEventAtTime(_state.playheadSeconds, -1);

            e.Use();
            return;
        }

        // Duplicate selected
        if (e.keyCode == KeyCode.D)
        {
            if (HasValidSelectedEvent())
            {
                DuplicateEventAtIndex(_selectedEventIndex);
                e.Use();
            }
            return;
        }

        // Undo / Redo
        if (e.keyCode == KeyCode.Z)
        {
            if (e.shift)
                Undo.PerformRedo();
            else
                Undo.PerformUndo();

            e.Use();
            return;
        }

        if (e.keyCode == KeyCode.Y)
        {
            Undo.PerformRedo();
            e.Use();
            return;
        }
    }

    void PasteClipboardEventAtTime(float seconds, int insertAfterIndex = -1)
    {
        if (_manager == null || _manager.events == null)
            return;

        if (!AnimatorStateEventEditorClipboard.TryGetEventClone(out AnimatorStateEvent clone) || clone == null)
            return;

        float t = _clip != null
            ? ClampAndSnapTimelineSeconds(seconds, Mathf.Max(0.01f, _clip.length))
            : Mathf.Max(0f, seconds);

        if (_clip != null)
            ApplyEventTime(clone, t);

        Undo.RecordObject(_manager, "Paste Animator State Event");

        int insertAt;
        if (insertAfterIndex >= 0 && insertAfterIndex < _manager.events.Count)
            insertAt = Mathf.Clamp(insertAfterIndex + 1, 0, _manager.events.Count);
        else
            insertAt = _manager.events.Count;

        _manager.events.Insert(insertAt, clone);
        _state.playheadSeconds = t;

        SortAndKeepTrackedEvent(clone);

        EditorUtility.SetDirty(_manager);
        RebuildCategories();
        SaveStateSoon();
        Repaint();
    }

    void DrawMarkerHoverLabel(Rect laneRect, Rect markerRect, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        GUIStyle style = EditorStyles.miniLabel;
        Vector2 size = style.CalcSize(new GUIContent(text));

        float padX = 4f;
        float padY = 1f;
        float w = Mathf.Min(laneRect.width - 8f, size.x + padX * 2f);
        float h = Mathf.Max(16f, size.y + padY * 2f);

        float x = markerRect.x + 10f;
        float y = Mathf.Max(laneRect.y + 1f, markerRect.y - 1f);

        if (x + w > laneRect.xMax - 2f)
            x = Mathf.Max(laneRect.x + 2f, laneRect.xMax - w - 2f);

        Rect bg = new Rect(x, y - 3, w, h);
        Rect fg = new Rect(bg.x + padX, bg.y + padY, bg.width - padX * 2f, bg.height - padY * 2f);

        Color bgColor = EditorGUIUtility.isProSkin
            ? new Color(0f, 0f, 0f, 0.9f)
            : new Color(1f, 1f, 1f, 1f);

        Color borderColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.12f)
            : new Color(0f, 0f, 0f, 0.14f);

        EditorGUI.DrawRect(bg, bgColor);
        EditorGUI.DrawRect(new Rect(bg.x, bg.y, bg.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(bg.x, bg.yMax - 1f, bg.width, 1f), borderColor);
        EditorGUI.DrawRect(new Rect(bg.x, bg.y, 1f, bg.height), borderColor);
        EditorGUI.DrawRect(new Rect(bg.xMax - 1f, bg.y, 1f, bg.height), borderColor);

        GUI.Label(fg, text, style);
    }

    void DrawSelectedEventPreviewPane()
    {
        float maxH = Mathf.Max(EventPreviewMinHeight, position.height - 180f);
        _state.eventPreviewHeight = Mathf.Clamp(_state.eventPreviewHeight, EventPreviewMinHeight, Mathf.Min(EventPreviewMaxHeight, maxH));

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(_state.eventPreviewHeight)))
        {
            SerializedObject so;
            SerializedProperty element;
            AnimatorStateEvent evt;
            bool hasSelected = TryGetSelectedEventSerialized(out so, out element, out evt);

            if (hasSelected && so != null)
                so.UpdateIfRequiredOrScript();

            EditorGUI.BeginChangeCheck();

            Rect headerRect = GUILayoutUtility.GetRect(10f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            DrawSelectedEventPreviewHeader(headerRect, hasSelected ? element : null, evt);

            _selectedEventPreviewScroll = EditorGUILayout.BeginScrollView(_selectedEventPreviewScroll, GUILayout.ExpandHeight(true));
            try
            {
                if (!hasSelected)
                {
                    EditorGUILayout.HelpBox("Select an event marker in the timeline to preview/edit its properties.", MessageType.Info);
                }
                else
                {
                    DrawSelectedEventPreviewBody(element);
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }

            if (hasSelected && so != null)
            {
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.IncrementCurrentGroup();
                    int group = Undo.GetCurrentGroup();
                    Undo.SetCurrentGroupName("Edit Animator State Event");
                    Undo.RegisterCompleteObjectUndo(_manager, "Edit Animator State Event");

                    AnimatorStateEvent tracked = evt;

                    so.ApplyModifiedProperties();
                    SortAndKeepTrackedEvent(tracked);

                    Undo.CollapseUndoOperations(group);

                    EditorUtility.SetDirty(_manager);
                    RebuildCategories();
                    SaveStateSoon();
                    Repaint();
                }
                else
                {
                    so.ApplyModifiedProperties();
                }
            }
            else
            {
                EditorGUI.EndChangeCheck();
            }
        }
    }

    void DrawEventPreviewResizeHandle()
    {
        Rect lineRect = GUILayoutUtility.GetRect(10f, EventPreviewSplitterHeight, GUILayout.ExpandWidth(true));

        Rect hitRect = lineRect;
        hitRect.yMin -= 4f;
        hitRect.yMax += 4f;

        EditorGUIUtility.AddCursorRect(hitRect, MouseCursor.ResizeVertical);

        Color line = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.12f);
        EditorGUI.DrawRect(lineRect, line);

        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button == 0 && hitRect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    _isResizingEventPreview = true;
                    _eventPreviewResizeStartMouseY = e.mousePosition.y;
                    _eventPreviewResizeStartHeight = _state.eventPreviewHeight;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id && _isResizingEventPreview)
                {
                    float delta = e.mousePosition.y - _eventPreviewResizeStartMouseY;
                    float maxH = Mathf.Max(EventPreviewMinHeight, position.height - 180f);

                    _state.eventPreviewHeight = Mathf.Clamp(
                        _eventPreviewResizeStartHeight + delta,
                        EventPreviewMinHeight,
                        Mathf.Min(EventPreviewMaxHeight, maxH));

                    SaveStateSoon();
                    Repaint();
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    _isResizingEventPreview = false;
                    SaveStateSoon();
                    e.Use();
                }
                break;
        }
    }

    void DrawSelectedEventPreviewHeader(Rect rect, SerializedProperty element, AnimatorStateEvent evt)
    {
        var enabledProp = element != null ? element.FindPropertyRelative("enabled") : null;

        if (element != null)
            DrawSelectedEventHeaderBackground(rect, element, enabledProp);
        else
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.02f) : new Color(0f, 0f, 0f, 0.03f));

        Rect toggleRect = new Rect(rect.xMax - EventPreviewToggleW, rect.y, EventPreviewToggleW, rect.height);
        Rect menuRect = new Rect(toggleRect.xMin - EventPreviewToggleGap - EventPreviewBtnW, rect.y, EventPreviewBtnW, rect.height);
        Rect labelRect = new Rect(rect.x + 6f, rect.y, Mathf.Max(20f, menuRect.xMin - rect.x - 8f), rect.height);

        if (element == null || evt == null)
        {
            EditorGUI.LabelField(labelRect, "Selected Event Preview", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                GUI.Button(menuRect, "⋮", EditorStyles.miniButton);
                GUI.Toggle(toggleRect, false, GUIContent.none);
            }
            return;
        }

        string title = AnimatorStateEventEditorPresentation.GetDisplayName(evt);
        string category = AnimatorStateEventEditorPresentation.GetCategory(evt);
        string headerLabel = string.IsNullOrEmpty(category)
            ? title
            : ("[" + category + "] " + title);

        EditorGUI.LabelField(labelRect, headerLabel, EditorStyles.boldLabel);

        if (GUI.Button(menuRect, "⋮", EditorStyles.miniButton))
            ShowSelectedEventPreviewMenu();

        if (enabledProp != null)
            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);
    }

    void ShowSelectedEventPreviewMenu()
    {
        if (!HasValidSelectedEvent())
            return;

        int index = _selectedEventIndex;
        AnimatorStateEvent evt = _manager.events[index];

        GenericMenu menu = new GenericMenu();
        menu.AddDisabledItem(new GUIContent(evt != null ? AnimatorStateEventEditorPresentation.GetDisplayName(evt) : "Event"));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Set Playhead To Event"), false, () =>
        {
            if (!HasValidSelectedEvent()) return;
            float t = QuantizeSecondsToClipFrame(GetEventApproxTriggerSeconds(_manager.events[_selectedEventIndex]));
            _state.playheadSeconds = t;
            SaveStateSoon();
            Repaint();
        });

        menu.AddSeparator("");

        // NEW: open/edit script
        if (evt != null)
            menu.AddItem(new GUIContent("Edit Event Script"), false, () => OpenEventScript(evt.GetType()));
        else
            menu.AddDisabledItem(new GUIContent("Edit Event Script"));

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Copy Event"), false, () => CopyEventAtIndex(index));

        if (AnimatorStateEventEditorClipboard.HasEvent)
            menu.AddItem(new GUIContent("Paste Event (Replace)"), false, () => PasteEventReplaceAtIndex(index));
        else
            menu.AddDisabledItem(new GUIContent("Paste Event (Replace)"));

        if (AnimatorStateEventEditorClipboard.HasEvent)
            menu.AddItem(new GUIContent("Paste Event (Insert After)"), false, () => PasteEventAfterIndex(index));
        else
            menu.AddDisabledItem(new GUIContent("Paste Event (Insert After)"));

        menu.AddItem(new GUIContent("Duplicate Event"), false, () => DuplicateEventAtIndex(index));
        menu.AddItem(new GUIContent("Delete Event"), false, () => DeleteEventAtIndex(index));

        menu.ShowAsContext();
    }
    
    bool HasValidSelectedEvent()
    {
        return _manager != null &&
               _manager.events != null &&
               _selectedEventIndex >= 0 &&
               _selectedEventIndex < _manager.events.Count &&
               _manager.events[_selectedEventIndex] != null;
    }

    bool TryGetSelectedEventSerialized(out SerializedObject so, out SerializedProperty element, out AnimatorStateEvent evt)
    {
        so = null;
        element = null;
        evt = null;

        if (!HasValidSelectedEvent())
            return false;

        evt = _manager.events[_selectedEventIndex];
        if (evt == null)
            return false;

        so = new SerializedObject(_manager);
        SerializedProperty eventsProp = so.FindProperty("events");
        if (eventsProp == null || !eventsProp.isArray || _selectedEventIndex < 0 || _selectedEventIndex >= eventsProp.arraySize)
            return false;

        element = eventsProp.GetArrayElementAtIndex(_selectedEventIndex);
        return element != null;
    }

    static bool IsDrawerBaseFieldName(string name)
    {
        return name == "enabled" ||
               name == "timeMode" ||
               name == "normalizedTime" ||
               name == "seconds" ||
               name == "frame" ||
               name == "frequency" ||
               name == "runOnInterrupt";
    }

    static bool IsManagedReferenceMetaProperty(SerializedProperty p)
    {
        if (p == null)
            return true;

        string n = p.name;
        if (n == "managedReferenceFullTypename" ||
            n == "managedReferenceFieldTypename" ||
            n == "managedReferenceId" ||
            n == "managedReferenceData")
            return true;

        return false;
    }

    static void ClampNormalizedPreview(SerializedProperty normalized)
    {
        if (normalized == null) return;
        float v = normalized.floatValue;
        float c = Mathf.Clamp01(v);
        if (!Mathf.Approximately(v, c))
            normalized.floatValue = c;
    }

    static void ClampSecondsPreview(SerializedProperty seconds, AnimationClip clip)
    {
        if (seconds == null) return;
        float v = seconds.floatValue;
        float max = (clip != null && clip.length > 0f) ? clip.length : float.PositiveInfinity;
        float c = Mathf.Clamp(v, 0f, max);
        if (!Mathf.Approximately(v, c))
            seconds.floatValue = c;
    }

    static void ClampFramePreview(SerializedProperty frame, AnimationClip clip)
    {
        if (frame == null) return;
        int v = frame.intValue;
        int max = (clip != null) ? AnimatorStateEventEditorClipResolver.GetMaxFrame(clip) : int.MaxValue;
        int c = Mathf.Clamp(v, 0, max);
        if (v != c)
            frame.intValue = c;
    }

    void DrawSelectedEventHeaderBackground(Rect headerRect, SerializedProperty property, SerializedProperty enabledProp)
    {
        if (!AnimatorStateEventEditorPresentation.TryGetHeaderColor(property, out var color))
            return;

        bool isEnabled = enabledProp == null || enabledProp.boolValue;
        float bgAlpha = EditorGUIUtility.isProSkin ? 0.14f : 0.10f;
        float lineAlpha = EditorGUIUtility.isProSkin ? 0.70f : 0.55f;

        if (!isEnabled)
        {
            bgAlpha *= 0.45f;
            lineAlpha *= 0.55f;
        }

        Color fill = color;
        fill.a = bgAlpha;
        EditorGUI.DrawRect(headerRect, fill);

        Color accent = color;
        accent.a = lineAlpha;
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, EventPreviewHeaderAccentW, headerRect.height), accent);
    }

    void DrawSelectedEventPreviewBody(SerializedProperty element)
    {
        if (element == null)
        {
            EditorGUILayout.HelpBox("Selected event property is invalid.", MessageType.Warning);
            return;
        }

        if (element.propertyType == SerializedPropertyType.ManagedReference && element.managedReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Selected event reference is null.", MessageType.Warning);
            return;
        }

        element.isExpanded = true;

        AnimationClip clip = _clip;

        SerializedProperty timeMode = element.FindPropertyRelative("timeMode");
        SerializedProperty normalized = element.FindPropertyRelative("normalizedTime");
        SerializedProperty seconds = element.FindPropertyRelative("seconds");
        SerializedProperty frame = element.FindPropertyRelative("frame");
        SerializedProperty frequency = element.FindPropertyRelative("frequency");
        SerializedProperty runOnInterrupt = element.FindPropertyRelative("runOnInterrupt");

        EditorGUILayout.Space(2f);

        if (timeMode != null)
            EditorGUILayout.PropertyField(timeMode, true);

        if (timeMode != null)
        {
            var mode = (AnimatorEventTimeMode)timeMode.enumValueIndex;

            if (mode == AnimatorEventTimeMode.Seconds)
            {
                if (seconds != null) EditorGUILayout.PropertyField(seconds, true);
                ClampSecondsPreview(seconds, clip);
            }
            else if (mode == AnimatorEventTimeMode.ClipFrame)
            {
                if (frame != null) EditorGUILayout.PropertyField(frame, true);
                ClampFramePreview(frame, clip);
            }
            else
            {
                if (normalized != null) EditorGUILayout.PropertyField(normalized, true);
                ClampNormalizedPreview(normalized);
            }
        }

        if (frequency != null)
            EditorGUILayout.PropertyField(frequency, true);

        if (runOnInterrupt != null)
            EditorGUILayout.PropertyField(runOnInterrupt, true);

        DrawSelectedEventRemainingFields(element);
    }

    void DrawSelectedEventRemainingFields(SerializedProperty root)
    {
        var it = root.Copy();
        var end = it.GetEndProperty();

        bool enter = true;
        bool drewAny = false;

        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            enter = false;

            if (it.depth != root.depth + 1)
                continue;

            if (IsDrawerBaseFieldName(it.name))
                continue;

            if (IsManagedReferenceMetaProperty(it))
                continue;

            EditorGUILayout.PropertyField(it, true);
            drewAny = true;
        }

        if (!drewAny)
        {
            // no-op; base fields may be the only fields
        }
    }

    void DrawTopToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(ToolbarHeight)))
        {
            EditorGUI.BeginChangeCheck();
            var newManager = (AnimatorStateEventManager)EditorGUILayout.ObjectField(
                _manager,
                typeof(AnimatorStateEventManager),
                true,
                GUILayout.Width(280f));

            if (EditorGUI.EndChangeCheck())
            {
                SetManager(newManager);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshClipBinding();
                RebuildCategories();
                Repaint();
            }

            GUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(_clip == null))
            {
                if (GUILayout.Button("Select Clip", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    Selection.activeObject = _clip;
            }

            GUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(_manager == null || _clip == null))
            {
                if (GUILayout.Button("Add Event", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                    ShowAddEventMenuAtTime(_state.playheadSeconds);
            }

            GUILayout.FlexibleSpace();

            EditorGUI.BeginChangeCheck();
            bool displayFrames = GUILayout.Toggle(_state.displayFrames, _state.displayFrames ? "Frames" : "Seconds", EditorStyles.toolbarButton, GUILayout.Width(72f));
            if (EditorGUI.EndChangeCheck())
            {
                _state.displayFrames = displayFrames;
                SaveStateSoon();
                Repaint();
            }

            EditorGUI.BeginChangeCheck();
            bool snap = GUILayout.Toggle(_state.snapToFrames, "Snap To Frames", EditorStyles.toolbarButton, GUILayout.Width(110f));
            if (EditorGUI.EndChangeCheck())
            {
                _state.snapToFrames = snap;
                RebuildCategories();
                SaveStateSoon();
                Repaint();
            }

            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Zoom", GUILayout.Width(34f));

            EditorGUI.BeginChangeCheck();
            float zoom = GUILayout.HorizontalSlider(_state.zoomPxPerSecond, MinZoom, MaxZoom, GUILayout.Width(140f));
            if (EditorGUI.EndChangeCheck())
            {
                _state.zoomPxPerSecond = zoom;
                SaveStateSoon();
                Repaint();
            }
        }
    }

    void DrawNoManagerUI()
    {
        EditorGUILayout.HelpBox("Assign an AnimatorStateEventManager to open the timeline editor.", MessageType.Info);
    }

    void DrawMissingClipUI()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("No clip resolved", EditorStyles.boldLabel);

            if (_manager != null)
                EditorGUILayout.ObjectField("Manager", _manager, typeof(AnimatorStateEventManager), true);

            if (!string.IsNullOrEmpty(_statePath))
                EditorGUILayout.LabelField("State", _statePath);

            EditorGUILayout.LabelField("The state may not have a clip motion, or the resolver could not find the state.");

            GUILayout.Space(4f);

            if (GUILayout.Button("Refresh Clip Binding"))
            {
                RefreshClipBinding();
                RebuildCategories();
                Repaint();
            }
        }
    }

    void DrawTimelineArea()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);

            float lanesHeight = Mathf.Max(DefaultTimelineMinHeight, GetTotalLanesHeight());
            Rect totalRect = GUILayoutUtility.GetRect(10f, RulerHeight + lanesHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(totalRect, EditorGUIUtility.isProSkin ? new Color(0.10f, 0.10f, 0.10f) : new Color(0.92f, 0.92f, 0.92f));

            float clipLength = Mathf.Max(0.01f, _clip.length);
            float timelineWidth = Mathf.Max(totalRect.width - LaneHeaderWidth, clipLength * _state.zoomPxPerSecond + 40f);

            Rect leftRect = new Rect(totalRect.x, totalRect.y, LaneHeaderWidth, totalRect.height);
            Rect rightRect = new Rect(totalRect.x + LaneHeaderWidth, totalRect.y, Mathf.Max(8f, totalRect.width - LaneHeaderWidth), totalRect.height);

            Rect rulerViewportRect = new Rect(rightRect.x, rightRect.y, rightRect.width, RulerHeight);
            Rect lanesViewportRect = new Rect(rightRect.x, rightRect.y + RulerHeight, rightRect.width, Mathf.Max(8f, rightRect.height - RulerHeight));

            HandleTimelineWheelInput(totalRect, leftRect, rightRect, timelineWidth, lanesHeight);

            DrawStickyTimelineRuler(rulerViewportRect, timelineWidth, clipLength);

            Rect laneViewRect = new Rect(0f, 0f, timelineWidth, lanesHeight);
            _scroll = GUI.BeginScrollView(lanesViewportRect, _scroll, laneViewRect, true, true);

            DrawTimelineLanes(new Rect(0f, 0f, timelineWidth, lanesHeight));
            DrawPlayhead(new Rect(0f, 0f, timelineWidth, lanesHeight), clipLength);
            HandleTimelineInteractions(new Rect(0f, 0f, timelineWidth, lanesHeight), clipLength);

            GUI.EndScrollView();

            ClampScroll(lanesViewportRect, laneViewRect);
            DrawStickyCategoryColumn(leftRect, lanesHeight);

            if (GUI.changed)
                SaveStateSoon();
        }
    }

    void DrawStatePathBanner()
    {
        if (_manager == null)
            return;

        string text = string.IsNullOrWhiteSpace(_statePath)
            ? "Path: Unknown"
            : "Path: " + _statePath;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }
    }

    void DrawStickyTimelineRuler(Rect viewportRect, float timelineWidth, float clipLength)
    {
        if (viewportRect.width <= 0f || viewportRect.height <= 0f)
            return;

        GUI.BeginClip(viewportRect);

        Rect rulerRect = new Rect(-_scroll.x, 0f, timelineWidth, RulerHeight);
        DrawTimelineRuler(rulerRect, clipLength);
        DrawPlayhead(new Rect(-_scroll.x, 0f, timelineWidth, RulerHeight), clipLength);
        HandleRulerInteractions(new Rect(-_scroll.x, 0f, timelineWidth, RulerHeight), clipLength);

        GUI.EndClip();
    }

    void DrawTimelineRuler(Rect rect, float clipLength)
    {
        EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.84f, 0.84f, 0.84f));

        float pixelsPerSecond = _state.zoomPxPerSecond;
        int maxFrame = AnimatorStateEventEditorClipResolver.GetMaxFrame(_clip);

        if (_state.displayFrames)
        {
            float frameDuration = GetFrameDuration(_clip);
            int majorStepFrames = Mathf.Max(1, GetMajorFrameStep(pixelsPerSecond * frameDuration));

            for (int f = 0; f <= maxFrame; f += majorStepFrames)
            {
                float x = rect.x + FrameToSeconds(f, _clip) * pixelsPerSecond;
                EditorGUI.DrawRect(new Rect(x, rect.y + 8f, 1f, rect.height - 8f), GridColorMajor());
                GUI.Label(new Rect(x + 3f, rect.y + 2f, 84f, 16f), f.ToString(), EditorStyles.miniLabel);
            }

            if (_state.snapToFrames)
            {
                for (int f = 0; f <= maxFrame; f++)
                {
                    if (f % majorStepFrames == 0) continue;
                    float x = rect.x + FrameToSeconds(f, _clip) * pixelsPerSecond;
                    EditorGUI.DrawRect(new Rect(x, rect.y + 14f, 1f, rect.height - 14f), GridColorMinor());
                }
            }
        }
        else
        {
            float totalSeconds = clipLength;
            float majorStepSec = GetMajorSecondStep(pixelsPerSecond);

            for (float t = 0f; t <= totalSeconds + 0.0001f; t += majorStepSec)
            {
                float x = rect.x + t * pixelsPerSecond;
                EditorGUI.DrawRect(new Rect(x, rect.y + 8f, 1f, rect.height - 8f), GridColorMajor());
                GUI.Label(new Rect(x + 3f, rect.y + 2f, 90f, 16f), t.ToString("0.###", CultureInfo.InvariantCulture) + "s", EditorStyles.miniLabel);
            }

            if (_state.snapToFrames)
            {
                for (int f = 0; f <= maxFrame; f++)
                {
                    float x = rect.x + FrameToSeconds(f, _clip) * pixelsPerSecond;
                    EditorGUI.DrawRect(new Rect(x, rect.y + 16f, 1f, rect.height - 16f), GridColorMinor());
                }
            }
        }
    }

    void DrawTimelineLanes(Rect rect)
    {
        _markerHits.Clear();

        _hasDeferredMarkerHover = false;
        _deferredMarkerHoverText = null;
        _deferredMarkerHoverLaneRect = default;
        _deferredMarkerHoverMarkerRect = default;

        float y = rect.y;

        if (_categories.Count == 0)
        {
            Rect lane = new Rect(rect.x, y, rect.width, _laneRowHeight);
            EditorGUI.DrawRect(lane, LaneFill(0));
            GUI.Label(new Rect(lane.x + 6f, lane.y + 3f, 160f, lane.height - 6f), "No event categories", EditorStyles.miniLabel);
            return;
        }

        for (int i = 0; i < _categories.Count; i++)
        {
            CategoryInfo cat = _categories[i];
            float laneHeight = GetLaneHeight(cat);

            Rect lane = new Rect(rect.x, y, rect.width, laneHeight);
            EditorGUI.DrawRect(lane, LaneFill(i));

            DrawLaneFrameGrid(lane);
            DrawCategoryMarkers(cat, lane);

            y += laneHeight;
        }

        // Draw hover tooltip LAST so it appears over all markers.
        if (_hasDeferredMarkerHover && Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(_deferredMarkerHoverText))
        {
            DrawMarkerHoverLabel(_deferredMarkerHoverLaneRect, _deferredMarkerHoverMarkerRect, _deferredMarkerHoverText);
        }
    }

    void DrawCategoryMarkers(CategoryInfo cat, Rect laneRect)
    {
        if (cat == null || cat.markers == null)
            return;

        for (int i = 0; i < cat.markers.Count; i++)
        {
            MarkerLayoutInfo m = cat.markers[i];

            if (_manager == null || _manager.events == null || m.eventIndex < 0 || m.eventIndex >= _manager.events.Count)
                continue;

            AnimatorStateEvent evt = _manager.events[m.eventIndex];
            if (evt == null)
                continue;

            float x = laneRect.x + m.snappedSeconds * _state.zoomPxPerSecond;
            float rowY = laneRect.y + MarkerRowPadding + m.stackRow * _laneRowHeight;
            float markerH = Mathf.Max(8f, _laneRowHeight - MarkerRowPadding * 2f);

            Rect marker = new Rect(x - MarkerWidth * 0.5f, rowY, MarkerWidth, markerH);

            bool enabled = evt.enabled;
            bool runOnInterrupt = evt.runOnInterrupt;

            Color baseColor;
            if (!AnimatorStateEventEditorPresentation.TryGetHeaderColor(evt.GetType(), out baseColor))
                baseColor = cat.color;

            if (!enabled)
            {
                baseColor = Color.Lerp(baseColor, new Color(0.45f, 0.45f, 0.45f, 1f), 0.65f);
                baseColor.a = 0.33f;
            }

            EditorGUI.DrawRect(marker, baseColor);

            // RunOnInterrupt indicato
            if (runOnInterrupt)
            {
                float size = Mathf.Min(marker.width, marker.height) * 0.5f;
                float borderThickness = 2.4f;

                // Center horizontally, anchor to bottom
                float squareX = marker.x + (marker.width - size) * 0.5f;
                float squareY = marker.yMax - size - 2f; // small padding from bottom

                Rect square = new Rect(squareX, squareY, size, size);

                Rect border = new Rect(
                    square.x - borderThickness,
                    square.y - borderThickness,
                    square.width + borderThickness * 2f,
                    square.height + borderThickness * 2f
                );

                Color accent = Color.red;
                accent.a = enabled ? 1f : 0.5f;

                EditorGUI.DrawRect(border, Color.black);
                EditorGUI.DrawRect(square, accent);
            }

            // Disabled indicator
            if (!enabled)
            {
                float size = Mathf.Min(marker.width, marker.height) * 0.5f;
                float borderThickness = 2.4f;

                // Center horizontally, anchor to bottom
                float squareX = marker.x + (marker.width - size) * 0.5f;
                float squareY = marker.yMax - size - 2f; // small padding from bottom

                Rect square = new Rect(squareX, squareY, size, size);

                Rect border = new Rect(
                    square.x - borderThickness,
                    square.y - borderThickness,
                    square.width + borderThickness * 2f,
                    square.height + borderThickness * 2f
                );

                Color accent = Color.white;
                accent.a = enabled ? 1f : 0.5f;

                //EditorGUI.DrawRect(border, accent);
            }

            if (m.eventIndex == _selectedEventIndex)
            {
                Color outline = Color.white;
                outline.a = EditorGUIUtility.isProSkin ? 0.88f : 0.95f;

                EditorGUI.DrawRect(new Rect(marker.x - 1f, marker.y - 1f, marker.width + 2f, 1f), outline);
                EditorGUI.DrawRect(new Rect(marker.x - 1f, marker.yMax, marker.width + 2f, 1f), outline);
                EditorGUI.DrawRect(new Rect(marker.x - 1f, marker.y - 1f, 1f, marker.height + 2f), outline);
                EditorGUI.DrawRect(new Rect(marker.xMax, marker.y - 1f, 1f, marker.height + 2f), outline);
            }

            _markerHits.Add(new MarkerHitInfo
            {
                eventIndex = m.eventIndex,
                categoryKey = cat.key,
                snappedSeconds = m.snappedSeconds,
                rect = marker
            });

            if (Event.current.type == EventType.Repaint && marker.Contains(Event.current.mousePosition))
            {
                string suffix = "";
                if (!enabled) suffix += " [Disabled]";
                if (runOnInterrupt) suffix += " [Interrupt]";

                _hasDeferredMarkerHover = true;
                _deferredMarkerHoverLaneRect = laneRect;
                _deferredMarkerHoverMarkerRect = marker;
                _deferredMarkerHoverText = AnimatorStateEventEditorPresentation.GetDisplayName(evt) + " @ " + FormatTime(m.snappedSeconds) + suffix;
            }
        }
    }

    void DrawStickyCategoryColumn(Rect leftRect, float lanesHeight)
    {
        Rect rulerLeftRect = new Rect(leftRect.x, leftRect.y, leftRect.width, RulerHeight);
        EditorGUI.DrawRect(rulerLeftRect, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.84f, 0.84f, 0.84f));
        GUI.Label(new Rect(rulerLeftRect.x + 8f, rulerLeftRect.y + 5f, rulerLeftRect.width - 16f, 16f), "Categories", EditorStyles.miniLabel);

        Rect lanesViewport = new Rect(leftRect.x, leftRect.y + RulerHeight, leftRect.width, Mathf.Max(0f, leftRect.height - RulerHeight));
        EditorGUI.DrawRect(lanesViewport, EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.08f) : new Color(1f, 1f, 1f, 0.25f));

        GUI.BeginClip(lanesViewport);

        float y = -_scroll.y;

        if (_categories.Count == 0)
        {
            Rect lane = new Rect(0f, y, lanesViewport.width, _laneRowHeight);
            EditorGUI.DrawRect(lane, LaneFill(0));
            GUI.Label(new Rect(8f, y + 3f, lane.width - 12f, lane.height - 6f), "No event categories", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < _categories.Count; i++)
            {
                CategoryInfo cat = _categories[i];
                float laneHeight = GetLaneHeight(cat);

                Rect lane = new Rect(0f, y, lanesViewport.width, laneHeight);
                EditorGUI.DrawRect(lane, LaneFill(i));

                Color fill = cat.color;
                fill.a = EditorGUIUtility.isProSkin ? 0.18f : 0.15f;
                EditorGUI.DrawRect(lane, fill);

                Color accent = cat.color;
                accent.a = 0.92f;
                EditorGUI.DrawRect(new Rect(lane.x, lane.y, 3f, lane.height), accent);

                Rect labelRect = new Rect(8f, lane.y + 3f, lane.width - 16f, 16f);
                GUI.Label(labelRect, cat.display, EditorStyles.miniLabel);

                y += laneHeight;
            }
        }

        GUI.EndClip();

        Color divider = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.07f) : new Color(0f, 0f, 0f, 0.12f);
        EditorGUI.DrawRect(new Rect(leftRect.xMax - 1f, leftRect.y, 1f, leftRect.height), divider);
    }

    void DrawLaneFrameGrid(Rect rect)
    {
        if (_clip == null || !_state.snapToFrames)
            return;

        int maxFrame = AnimatorStateEventEditorClipResolver.GetMaxFrame(_clip);
        float pixelsPerSecond = _state.zoomPxPerSecond;

        for (int f = 0; f <= maxFrame; f++)
        {
            float x = rect.x + FrameToSeconds(f, _clip) * pixelsPerSecond;
            EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), GridColorMinorLane());
        }
    }

    void DrawPlayhead(Rect rect, float clipLength)
    {
        _state.playheadSeconds = Mathf.Clamp(_state.playheadSeconds, 0f, clipLength);

        float x = rect.x + _state.playheadSeconds * _state.zoomPxPerSecond;
        Color c = new Color(1f, 0.22f, 0.22f, 0.9f);

        EditorGUI.DrawRect(new Rect(x, rect.y, 1.5f, rect.height), c);
    }

    void HandleTimelineWheelInput(Rect totalRect, Rect leftRect, Rect rightRect, float timelineWidth, float lanesHeight)
    {
        Event e = Event.current;
        if (e.type != EventType.ScrollWheel)
            return;

        if (!totalRect.Contains(e.mousePosition))
            return;

        bool ctrl = e.control || e.command;

        if (ctrl)
        {
            float oldZoom = _state.zoomPxPerSecond;
            float newZoom = Mathf.Clamp(oldZoom + (-e.delta.y * 18f), MinZoom, MaxZoom);

            if (!Mathf.Approximately(oldZoom, newZoom))
            {
                if (rightRect.Contains(e.mousePosition))
                {
                    float localX = Mathf.Clamp(e.mousePosition.x - rightRect.x, 0f, rightRect.width);
                    float timeAtMouse = (_scroll.x + localX) / Mathf.Max(1f, oldZoom);

                    _state.zoomPxPerSecond = newZoom;
                    _scroll.x = Mathf.Max(0f, timeAtMouse * newZoom - localX);
                }
                else
                {
                    _state.zoomPxPerSecond = newZoom;
                }

                SaveStateSoon();
                Repaint();
            }

            e.Use();
            return;
        }

        float wheelStep = e.delta.y * 24f;

        if (leftRect.Contains(e.mousePosition))
        {
            _scroll.y = Mathf.Max(0f, _scroll.y + wheelStep);
            ClampScroll(
                new Rect(rightRect.x, rightRect.y + RulerHeight, rightRect.width, Mathf.Max(8f, rightRect.height - RulerHeight)),
                new Rect(0f, 0f, timelineWidth, lanesHeight));

            SaveStateSoon();
            Repaint();
            e.Use();
            return;
        }

        if (rightRect.Contains(e.mousePosition))
        {
            _scroll.x = Mathf.Max(0f, _scroll.x + wheelStep);
            ClampScroll(
                new Rect(rightRect.x, rightRect.y + RulerHeight, rightRect.width, Mathf.Max(8f, rightRect.height - RulerHeight)),
                new Rect(0f, 0f, timelineWidth, lanesHeight));

            SaveStateSoon();
            Repaint();
            e.Use();
        }
    }

    void ClampScroll(Rect viewportRect, Rect viewRect)
    {
        float maxX = Mathf.Max(0f, viewRect.width - viewportRect.width + 16f);
        float maxY = Mathf.Max(0f, viewRect.height - viewportRect.height + 16f);

        _scroll.x = Mathf.Clamp(_scroll.x, 0f, maxX);
        _scroll.y = Mathf.Clamp(_scroll.y, 0f, maxY);
    }

    void HandleRulerInteractions(Rect rulerRect, float clipLength)
    {
        Event e = Event.current;
        bool mouseInRuler = rulerRect.Contains(e.mousePosition);

        if (e.type == EventType.MouseDown && e.button == 0 && mouseInRuler)
        {
            _pointerDownMousePos = e.mousePosition;
            _pendingPlayheadClipLength = clipLength;
            _pendingPlayheadTimelineRect = rulerRect;

            _pendingPlayheadDrag = true;
            _pendingMarkerDrag = false;
            _pendingMarkerHit = null;

            e.Use();
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (_pendingPlayheadDrag)
            {
                if ((e.mousePosition - _pointerDownMousePos).sqrMagnitude >= DragStartThresholdPx * DragStartThresholdPx)
                {
                    BeginPlayheadDrag(_pendingPlayheadTimelineRect, _pendingPlayheadClipLength, e.mousePosition, true);
                    _pendingPlayheadDrag = false;
                }

                e.Use();
                return;
            }

            if (_isDraggingPlayhead)
            {
                UpdatePlayheadDrag(rulerRect, clipLength, e.mousePosition);
                e.Use();
                return;
            }
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (_pendingPlayheadDrag)
            {
                _pendingPlayheadDrag = false;
                BeginPlayheadDrag(_pendingPlayheadTimelineRect, _pendingPlayheadClipLength, e.mousePosition, true);
                EndPointerDrag();
                e.Use();
                return;
            }

            if (_isDraggingPlayhead)
            {
                EndPointerDrag();
                e.Use();
                return;
            }
        }

        if (e.rawType == EventType.MouseUp && _isDraggingPlayhead)
            EndPointerDrag();
    }

    void HandleTimelineInteractions(Rect timelineRect, float clipLength)
    {
        Event e = Event.current;
        bool mouseInTimeline = timelineRect.Contains(e.mousePosition);

        if (e.type == EventType.MouseDown && e.button == 0 && mouseInTimeline)
        {
            _pointerDownMousePos = e.mousePosition;
            _pendingPlayheadClipLength = clipLength;
            _pendingPlayheadTimelineRect = timelineRect;

            if (TryGetMarkerHitAt(e.mousePosition, out MarkerHitInfo markerHit))
            {
                SetSelectedEventIndex(markerHit.eventIndex);
                _pendingMarkerDrag = true;
                _pendingPlayheadDrag = false;
                _pendingMarkerHit = markerHit;

                Repaint();
                e.Use();
                return;
            }

            SetSelectedEventIndex(-1);
            _pendingMarkerDrag = false;
            _pendingPlayheadDrag = true;
            _pendingMarkerHit = null;

            Repaint();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            if (_pendingMarkerDrag)
            {
                if ((e.mousePosition - _pointerDownMousePos).sqrMagnitude >= DragStartThresholdPx * DragStartThresholdPx)
                {
                    BeginEventDrag(_pendingMarkerHit, clipLength, e.mousePosition);
                    _pendingMarkerDrag = false;
                }

                e.Use();
                return;
            }

            if (_pendingPlayheadDrag)
            {
                if ((e.mousePosition - _pointerDownMousePos).sqrMagnitude >= DragStartThresholdPx * DragStartThresholdPx)
                {
                    BeginPlayheadDrag(_pendingPlayheadTimelineRect, _pendingPlayheadClipLength, e.mousePosition, true);
                    _pendingPlayheadDrag = false;
                }

                e.Use();
                return;
            }

            if (_isDraggingEvent)
            {
                UpdateEventDrag(timelineRect, clipLength, e.mousePosition);
                e.Use();
                return;
            }

            if (_isDraggingPlayhead)
            {
                UpdatePlayheadDrag(timelineRect, clipLength, e.mousePosition);
                e.Use();
                return;
            }
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (_pendingMarkerDrag)
            {
                _pendingMarkerDrag = false;
                _pendingMarkerHit = null;
                e.Use();
                return;
            }

            if (_pendingPlayheadDrag)
            {
                _pendingPlayheadDrag = false;
                BeginPlayheadDrag(_pendingPlayheadTimelineRect, _pendingPlayheadClipLength, e.mousePosition, true);
                EndPointerDrag();
                e.Use();
                return;
            }

            if (_isDraggingEvent || _isDraggingPlayhead)
            {
                EndPointerDrag();
                e.Use();
                return;
            }
        }

        if (e.rawType == EventType.MouseUp)
        {
            _pendingMarkerDrag = false;
            _pendingPlayheadDrag = false;
            _pendingMarkerHit = null;

            if (_isDraggingEvent || _isDraggingPlayhead)
                EndPointerDrag();
        }

        if (e.type == EventType.KeyDown)
        {
            if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && _selectedEventIndex >= 0)
            {
                DeleteEventAtIndex(_selectedEventIndex);
                e.Use();
                return;
            }

            if (e.keyCode == KeyCode.Escape)
            {
                EndPointerDrag();
                e.Use();
                return;
            }
        }

        if (e.type == EventType.ContextClick && mouseInTimeline)
        {
            _pendingMarkerDrag = false;
            _pendingPlayheadDrag = false;
            _pendingMarkerHit = null;

            if (TryGetMarkerHitAt(e.mousePosition, out MarkerHitInfo hit))
            {
                SetSelectedEventIndex(hit.eventIndex);
                ShowMarkerContextMenu(hit);
                e.Use();
                return;
            }

            float localX = Mathf.Max(0f, e.mousePosition.x - timelineRect.x);
            float t = ClampAndSnapTimelineSeconds(localX / Mathf.Max(1f, _state.zoomPxPerSecond), clipLength);

            ShowTimelineContextMenu(t);
            e.Use();
        }
    }

    void ShowTimelineContextMenu(float timelineSeconds)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddDisabledItem(new GUIContent("Timeline"));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Set Playhead Here"), false, () =>
        {
            _state.playheadSeconds = timelineSeconds;
            SaveStateSoon();
            Repaint();
        });

        menu.AddSeparator("");

        AppendAddEventMenuItems(menu, "Add Event", timelineSeconds, -1);

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Display/Seconds"), !_state.displayFrames, () =>
        {
            _state.displayFrames = false;
            SaveStateSoon();
            Repaint();
        });
        menu.AddItem(new GUIContent("Display/Frames"), _state.displayFrames, () =>
        {
            _state.displayFrames = true;
            SaveStateSoon();
            Repaint();
        });
        menu.AddItem(new GUIContent("Snap To Frames"), _state.snapToFrames, () =>
        {
            _state.snapToFrames = !_state.snapToFrames;
            RebuildCategories();
            SaveStateSoon();
            Repaint();
        });

        menu.ShowAsContext();
    }

    void OpenEventScript(Type eventType)
    {
        if (eventType == null)
            return;

        MonoScript script = FindMonoScriptForType(eventType);
        if (script == null)
        {
            Debug.LogWarning("Could not find script asset for event type: " + eventType.FullName);
            return;
        }

        Selection.activeObject = script;
        EditorGUIUtility.PingObject(script);
        AssetDatabase.OpenAsset(script);
    }

    MonoScript FindMonoScriptForType(Type t)
    {
        if (t == null)
            return null;

        string[] guids = AssetDatabase.FindAssets(t.Name + " t:MonoScript");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (ms == null)
                continue;

            Type cls = ms.GetClass();
            if (cls == t)
                return ms;
        }

        // Fallback broader search
        guids = AssetDatabase.FindAssets("t:MonoScript");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            MonoScript ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (ms == null)
                continue;

            Type cls = ms.GetClass();
            if (cls == t)
                return ms;
        }

        return null;
    }

    void BeginPlayheadDrag(Rect timelineRect, float clipLength, Vector2 mousePosition, bool setImmediately)
    {
        _isDraggingPlayhead = true;
        _isDraggingEvent = false;
        _draggingEventIndex = -1;

        if (setImmediately)
            UpdatePlayheadDrag(timelineRect, clipLength, mousePosition);
    }

    void UpdatePlayheadDrag(Rect timelineRect, float clipLength, Vector2 mousePosition)
    {
        float localX = Mathf.Max(0f, mousePosition.x - timelineRect.x);
        float t = localX / Mathf.Max(1f, _state.zoomPxPerSecond);

        _state.playheadSeconds = ClampAndSnapTimelineSeconds(t, clipLength);
        SaveStateSoon();
        Repaint();
    }

    void BeginEventDrag(MarkerHitInfo hit, float clipLength, Vector2 mousePosition)
    {
        if (_manager == null || _manager.events == null)
            return;

        if (hit == null || hit.eventIndex < 0 || hit.eventIndex >= _manager.events.Count)
            return;

        AnimatorStateEvent evt = _manager.events[hit.eventIndex];
        if (evt == null)
            return;

        float mouseSeconds = Mathf.Max(0f, mousePosition.x) / Mathf.Max(1f, _state.zoomPxPerSecond);
        _draggingEventMouseOffsetSeconds = mouseSeconds - hit.snappedSeconds;

        _isDraggingEvent = true;
        _isDraggingPlayhead = false;
        _draggingEventIndex = hit.eventIndex;

        Undo.IncrementCurrentGroup();
        _eventDragUndoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Move Animator State Event");
        Undo.RegisterCompleteObjectUndo(_manager, "Move Animator State Event");
    }

    void UpdateEventDrag(Rect timelineRect, float clipLength, Vector2 mousePosition)
    {
        if (_manager == null || _manager.events == null)
            return;

        if (_draggingEventIndex < 0 || _draggingEventIndex >= _manager.events.Count)
            return;

        AnimatorStateEvent evt = _manager.events[_draggingEventIndex];
        if (evt == null)
            return;

        float localX = mousePosition.x - timelineRect.x;
        if (timelineRect.width <= 0f)
            localX = mousePosition.x;

        float mouseSeconds = Mathf.Max(0f, localX) / Mathf.Max(1f, _state.zoomPxPerSecond);
        float desired = mouseSeconds - _draggingEventMouseOffsetSeconds;
        float snapped = ClampAndSnapTimelineSeconds(desired, clipLength);

        if (ApplyEventTime(evt, snapped))
        {
            _state.playheadSeconds = snapped;
            SortAndKeepTrackedEvent(evt, keepDraggingIndex: true);
            EditorUtility.SetDirty(_manager);
            RebuildCategories();
            SaveStateSoon();
            Repaint();
        }
    }

    void EndPointerDrag()
    {
        bool wasDraggingEvent = _isDraggingEvent;

        _pendingMarkerDrag = false;
        _pendingPlayheadDrag = false;
        _pendingMarkerHit = null;

        _isDraggingPlayhead = false;
        _isDraggingEvent = false;
        _draggingEventIndex = -1;

        if (wasDraggingEvent && _eventDragUndoGroup >= 0)
        {
            Undo.CollapseUndoOperations(_eventDragUndoGroup);
            _eventDragUndoGroup = -1;
        }

        SaveStateSoon();
        Repaint();
    }

    bool TryGetMarkerHitAt(Vector2 mousePosition, out MarkerHitInfo hit)
    {
        for (int i = _markerHits.Count - 1; i >= 0; i--)
        {
            MarkerHitInfo h = _markerHits[i];

            Rect pick = h.rect;
            pick.xMin -= 3f;
            pick.xMax += 3f;
            pick.yMin -= 1f;
            pick.yMax += 1f;

            if (pick.Contains(mousePosition))
            {
                hit = h;
                return true;
            }
        }

        hit = null;
        return false;
    }

    void ShowMarkerContextMenu(MarkerHitInfo hit)
    {
        GenericMenu menu = new GenericMenu();

        if (_manager == null || _manager.events == null || hit == null || hit.eventIndex < 0 || hit.eventIndex >= _manager.events.Count)
        {
            menu.AddDisabledItem(new GUIContent("Event"));
            menu.ShowAsContext();
            return;
        }

        SetSelectedEventIndex(hit.eventIndex);

        AnimatorStateEvent evt = _manager.events[hit.eventIndex];
        string title = evt != null ? AnimatorStateEventEditorPresentation.GetDisplayName(evt) : "Event";

        menu.AddDisabledItem(new GUIContent(title));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Set Playhead Here"), false, () =>
        {
            _state.playheadSeconds = hit.snappedSeconds;
            SaveStateSoon();
            Repaint();
        });

        menu.AddSeparator("");

        AppendAddEventMenuItems(menu, "Add Event/Insert After", hit.snappedSeconds, hit.eventIndex);

        menu.AddSeparator("");

        if (evt != null)
            menu.AddItem(new GUIContent("Edit Event Script"), false, () => OpenEventScript(evt.GetType()));
        else
            menu.AddDisabledItem(new GUIContent("Edit Event Script"));

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Copy Event"), false, () => CopyEventAtIndex(hit.eventIndex));

        if (AnimatorStateEventEditorClipboard.HasEvent)
            menu.AddItem(new GUIContent("Paste Event (Replace)"), false, () => PasteEventReplaceAtIndex(hit.eventIndex));
        else
            menu.AddDisabledItem(new GUIContent("Paste Event (Replace)"));

        if (AnimatorStateEventEditorClipboard.HasEvent)
            menu.AddItem(new GUIContent("Paste Event (Insert After)"), false, () => PasteEventAfterIndex(hit.eventIndex));
        else
            menu.AddDisabledItem(new GUIContent("Paste Event (Insert After)"));

        menu.AddItem(new GUIContent("Duplicate Event"), false, () => DuplicateEventAtIndex(hit.eventIndex));
        menu.AddItem(new GUIContent("Delete Event"), false, () => DeleteEventAtIndex(hit.eventIndex));

        menu.ShowAsContext();
    }

    void CopyEventAtIndex(int eventIndex)
    {
        if (_manager == null || _manager.events == null) return;
        if (eventIndex < 0 || eventIndex >= _manager.events.Count) return;

        AnimatorStateEvent evt = _manager.events[eventIndex];
        if (evt == null) return;

        AnimatorStateEventEditorClipboard.CopyEvent(evt);
    }

    void PasteEventReplaceAtIndex(int eventIndex)
    {
        if (_manager == null || _manager.events == null) return;
        if (eventIndex < 0 || eventIndex >= _manager.events.Count) return;
        if (!AnimatorStateEventEditorClipboard.TryGetEventClone(out AnimatorStateEvent clone) || clone == null) return;

        Undo.RecordObject(_manager, "Paste Animator State Event");
        _manager.events[eventIndex] = clone;

        SortAndKeepTrackedEvent(clone);

        EditorUtility.SetDirty(_manager);
        RebuildCategories();
        SaveStateSoon();
        Repaint();
    }

    void PasteEventAfterIndex(int eventIndex)
    {
        if (_manager == null || _manager.events == null) return;
        if (eventIndex < 0 || eventIndex >= _manager.events.Count) return;
        if (!AnimatorStateEventEditorClipboard.TryGetEventClone(out AnimatorStateEvent clone) || clone == null) return;

        Undo.RecordObject(_manager, "Paste Animator State Event");
        int insertAt = Mathf.Clamp(eventIndex + 1, 0, _manager.events.Count);
        _manager.events.Insert(insertAt, clone);

        SortAndKeepTrackedEvent(clone);

        EditorUtility.SetDirty(_manager);
        RebuildCategories();
        SaveStateSoon();
        Repaint();
    }

    void DuplicateEventAtIndex(int eventIndex)
    {
        if (_manager == null || _manager.events == null) return;
        if (eventIndex < 0 || eventIndex >= _manager.events.Count) return;

        AnimatorStateEvent src = _manager.events[eventIndex];
        if (src == null) return;

        AnimatorStateEvent clone = AnimatorStateEventEditorClipboard.CloneEvent(src);
        if (clone == null) return;

        Undo.RecordObject(_manager, "Duplicate Animator State Event");
        int insertAt = Mathf.Clamp(eventIndex + 1, 0, _manager.events.Count);
        _manager.events.Insert(insertAt, clone);

        SortAndKeepTrackedEvent(clone);

        EditorUtility.SetDirty(_manager);
        RebuildCategories();
        SaveStateSoon();
        Repaint();
    }

    void DeleteEventAtIndex(int eventIndex)
    {
        if (_manager == null || _manager.events == null)
            return;

        if (eventIndex < 0 || eventIndex >= _manager.events.Count)
            return;

        Undo.RecordObject(_manager, "Delete Animator State Event");
        _manager.events.RemoveAt(eventIndex);

        if (_selectedEventIndex == eventIndex)
            _selectedEventIndex = -1;
        else if (_selectedEventIndex > eventIndex)
            _selectedEventIndex--;

        if (_draggingEventIndex == eventIndex)
            _draggingEventIndex = -1;
        else if (_draggingEventIndex > eventIndex)
            _draggingEventIndex--;

        EditorUtility.SetDirty(_manager);
        RebuildCategories();
        SaveStateSoon();
        Repaint();
    }

    bool ApplyEventTime(AnimatorStateEvent evt, float seconds)
    {
        if (evt == null || _clip == null)
            return false;

        seconds = Mathf.Clamp(seconds, 0f, _clip.length);

        switch (evt.timeMode)
        {
            case AnimatorEventTimeMode.Seconds:
                {
                    if (Mathf.Approximately(evt.seconds, seconds))
                        return false;

                    evt.seconds = seconds;
                    return true;
                }

            case AnimatorEventTimeMode.ClipFrame:
                {
                    int maxFrame = AnimatorStateEventEditorClipResolver.GetMaxFrame(_clip);
                    int frame = Mathf.Clamp(SecondsToFrame(seconds, _clip), 0, maxFrame);

                    if (evt.frame == frame)
                        return false;

                    evt.frame = frame;
                    return true;
                }

            default:
                {
                    float n = _clip.length > 0f ? Mathf.Clamp01(seconds / _clip.length) : 0f;

                    if (Mathf.Approximately(evt.normalizedTime, n))
                        return false;

                    evt.normalizedTime = n;
                    return true;
                }
        }
    }

    float GetTotalLanesHeight()
    {
        if (_categories.Count == 0)
            return _laneRowHeight;

        float sum = 0f;
        for (int i = 0; i < _categories.Count; i++)
            sum += GetLaneHeight(_categories[i]);

        return sum;
    }

    float GetLaneHeight(CategoryInfo cat)
    {
        int rows = cat != null ? Mathf.Max(1, cat.stackRowCount) : 1;
        return Mathf.Max(_laneRowHeight, rows * _laneRowHeight + MarkerRowPadding * 2f);
    }

    void RebuildCategories()
    {
        _categories.Clear();

        if (_manager == null || _manager.events == null)
            return;

        var map = new Dictionary<string, CategoryInfo>(StringComparer.Ordinal);

        for (int i = 0; i < _manager.events.Count; i++)
        {
            AnimatorStateEvent evt = _manager.events[i];
            if (evt == null)
                continue;

            string category = GetTimelineCategory(evt);
            if (string.IsNullOrWhiteSpace(category))
                category = "Events";

            if (!map.TryGetValue(category, out CategoryInfo cat))
            {
                cat = new CategoryInfo
                {
                    key = category,
                    display = category,
                    color = DeriveCategoryColor(category)
                };

                map.Add(category, cat);
                _categories.Add(cat);
            }

            cat.eventIndices.Add(i);
        }

        for (int i = 0; i < _categories.Count; i++)
        {
            CategoryInfo cat = _categories[i];
            cat.display = $"{cat.key} [{cat.eventIndices.Count}]";
        }

        _categories.Sort((a, b) =>
        {
            string ak = a != null ? a.key : "";
            string bk = b != null ? b.key : "";
            return string.Compare(ak, bk, StringComparison.OrdinalIgnoreCase);
        });

        for (int i = 0; i < _categories.Count; i++)
            BuildMarkerLayout(_categories[i]);

        RefreshCategoryColorsFromEvents();

        if (_manager.events == null || _selectedEventIndex >= _manager.events.Count)
            _selectedEventIndex = -1;
    }

    void RefreshCategoryColorsFromEvents()
    {
        for (int i = 0; i < _categories.Count; i++)
        {
            CategoryInfo cat = _categories[i];
            if (cat == null)
                continue;

            Color fallback = DeriveCategoryColor(cat.key);
            cat.color = ComputeCategoryColorFromEvents(cat, fallback);
        }
    }

    Color ComputeCategoryColorFromEvents(CategoryInfo cat, Color fallback)
    {
        if (cat == null || cat.eventIndices == null || cat.eventIndices.Count == 0 || _manager == null || _manager.events == null)
            return fallback;

        float hueX = 0f;
        float hueY = 0f;
        float satSum = 0f;
        float valSum = 0f;
        float weightSum = 0f;

        for (int i = 0; i < cat.eventIndices.Count; i++)
        {
            int eventIndex = cat.eventIndices[i];
            if (eventIndex < 0 || eventIndex >= _manager.events.Count)
                continue;

            AnimatorStateEvent evt = _manager.events[eventIndex];
            if (evt == null)
                continue;

            Color eventColor;
            if (!AnimatorStateEventEditorPresentation.TryGetHeaderColor(evt.GetType(), out eventColor))
                continue;

            float weight = evt.enabled ? 1f : 0.35f;

            float h, s, v;
            Color.RGBToHSV(eventColor, out h, out s, out v);

            float angle = h * Mathf.PI * 2f;
            hueX += Mathf.Cos(angle) * weight;
            hueY += Mathf.Sin(angle) * weight;

            satSum += s * weight;
            valSum += v * weight;
            weightSum += weight;
        }

        if (weightSum <= 0f)
            return fallback;

        float hue;
        if (Mathf.Abs(hueX) < 0.0001f && Mathf.Abs(hueY) < 0.0001f)
            hue = 0f;
        else
        {
            hue = Mathf.Atan2(hueY, hueX) / (Mathf.PI * 2f);
            if (hue < 0f)
                hue += 1f;
        }

        float satAvg = satSum / weightSum;
        float valAvg = valSum / weightSum;

        // Keep lane colors calmer than marker colors.
        float sat = Mathf.Clamp01(Mathf.Lerp(satAvg, 0.32f, 0.35f));
        float val = EditorGUIUtility.isProSkin
            ? Mathf.Clamp01(Mathf.Max(valAvg, 0.72f))
            : Mathf.Clamp01(Mathf.Min(Mathf.Max(valAvg, 0.60f), 0.82f));

        Color mixed = Color.HSVToRGB(hue, sat, val);
        mixed = Color.Lerp(mixed, fallback, 0.15f); // small stabilizer so the lane color doesn't swing too hard
        mixed.a = 1f;

        return mixed;
    }

    void BuildMarkerLayout(CategoryInfo cat)
    {
        cat.markers.Clear();
        cat.stackRowCount = 1;

        if (cat.eventIndices == null || cat.eventIndices.Count == 0)
            return;

        List<MarkerLayoutInfo> items = new List<MarkerLayoutInfo>(cat.eventIndices.Count);

        for (int i = 0; i < cat.eventIndices.Count; i++)
        {
            int eventIndex = cat.eventIndices[i];

            if (_manager == null || _manager.events == null || eventIndex < 0 || eventIndex >= _manager.events.Count)
                continue;

            AnimatorStateEvent evt = _manager.events[eventIndex];
            if (evt == null)
                continue;

            float raw = GetEventApproxTriggerSeconds(evt);
            float snapped = QuantizeSecondsToClipFrame(raw);
            int frame = _clip != null ? SecondsToFrame(snapped, _clip) : Mathf.RoundToInt(snapped * 1000f);

            items.Add(new MarkerLayoutInfo
            {
                eventIndex = eventIndex,
                rawSeconds = raw,
                snappedSeconds = snapped,
                frame = frame,
                stackRow = 0
            });
        }

        items.Sort((a, b) =>
        {
            int c = a.frame.CompareTo(b.frame);
            if (c != 0) return c;
            return a.eventIndex.CompareTo(b.eventIndex);
        });

        Dictionary<int, int> frameOccupancy = new Dictionary<int, int>();
        for (int i = 0; i < items.Count; i++)
        {
            int used = 0;
            frameOccupancy.TryGetValue(items[i].frame, out used);

            items[i].stackRow = used;
            frameOccupancy[items[i].frame] = used + 1;
            cat.stackRowCount = Mathf.Max(cat.stackRowCount, used + 1);
        }

        cat.markers.AddRange(items);
    }

    static string GetTimelineCategory(AnimatorStateEvent evt)
    {
        if (evt == null)
            return "Events";

        string menuPath = AnimatorStateEventEditorPresentation.GetMenuPath(evt.GetType());
        if (string.IsNullOrWhiteSpace(menuPath))
            return "Events";

        int slash = menuPath.IndexOf('/');
        if (slash < 0)
            return menuPath.Trim();

        return menuPath.Substring(0, slash).Trim();
    }

    float GetEventApproxTriggerSeconds(AnimatorStateEvent evt)
    {
        if (evt == null || _clip == null)
            return 0f;

        switch (evt.timeMode)
        {
            case AnimatorEventTimeMode.Seconds:
                return Mathf.Clamp(evt.seconds, 0f, _clip.length);

            case AnimatorEventTimeMode.ClipFrame:
                return FrameToSeconds(Mathf.Max(0, evt.frame), _clip);

            default:
                return Mathf.Clamp01(evt.normalizedTime) * _clip.length;
        }
    }

    float ClampAndSnapTimelineSeconds(float t, float clipLength)
    {
        t = Mathf.Clamp(t, 0f, clipLength);
        return _state.snapToFrames ? QuantizeSecondsToClipFrame(t) : t;
    }

    float QuantizeSecondsToClipFrame(float seconds)
    {
        if (_clip == null || !_state.snapToFrames)
            return Mathf.Max(0f, seconds);

        return SnapSecondsToFrame(seconds, _clip);
    }

    public static float SnapSecondsToFrame(float seconds, AnimationClip clip)
    {
        if (clip == null || clip.frameRate <= 0f)
            return Mathf.Max(0f, seconds);

        float frame = seconds * clip.frameRate;
        int snappedFrame = Mathf.RoundToInt(frame);
        return FrameToSeconds(snappedFrame, clip);
    }

    static float FrameToSeconds(int frame, AnimationClip clip)
    {
        if (clip == null || clip.frameRate <= 0f)
            return 0f;

        frame = Mathf.Max(0, frame);
        return frame / clip.frameRate;
    }

    static int SecondsToFrame(float seconds, AnimationClip clip)
    {
        if (clip == null || clip.frameRate <= 0f)
            return 0;

        return Mathf.Max(0, Mathf.RoundToInt(seconds * clip.frameRate));
    }

    static float GetFrameDuration(AnimationClip clip)
    {
        if (clip == null || clip.frameRate <= 0f)
            return 1f / 60f;

        return 1f / clip.frameRate;
    }

    static int GetMajorFrameStep(float pxPerFrame)
    {
        if (pxPerFrame >= 18f) return 1;
        if (pxPerFrame >= 9f) return 2;
        if (pxPerFrame >= 5f) return 5;
        if (pxPerFrame >= 2.5f) return 10;
        if (pxPerFrame >= 1.25f) return 20;
        return 50;
    }

    static float GetMajorSecondStep(float pxPerSec)
    {
        if (pxPerSec >= 900f) return 0.05f;
        if (pxPerSec >= 500f) return 0.1f;
        if (pxPerSec >= 250f) return 0.25f;
        if (pxPerSec >= 120f) return 0.5f;
        if (pxPerSec >= 60f) return 1f;
        if (pxPerSec >= 30f) return 2f;
        if (pxPerSec >= 15f) return 5f;
        return 10f;
    }

    string FormatTime(float seconds)
    {
        if (_clip == null)
            return seconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";

        if (_state.displayFrames)
            return SecondsToFrame(seconds, _clip) + "f";

        return seconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";
    }

    static Color GridColorMajor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.15f)
            : new Color(0f, 0f, 0f, 0.16f);
    }

    static Color GridColorMinor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.08f)
            : new Color(0f, 0f, 0f, 0.08f);
    }

    static Color GridColorMinorLane()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, 0.035f)
            : new Color(0f, 0f, 0f, 0.035f);
    }

    Color LaneFill(int index)
    {
        bool even = (index & 1) == 0;
        return EditorGUIUtility.isProSkin
            ? (even ? new Color(1f, 1f, 1f, 0.025f) : new Color(1f, 1f, 1f, 0.045f))
            : (even ? new Color(0f, 0f, 0f, 0.02f) : new Color(0f, 0f, 0f, 0.035f));
    }

    static Color DeriveCategoryColor(string category)
    {
        if (string.IsNullOrEmpty(category))
            return new Color(0.55f, 0.65f, 0.85f, 1f);

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < category.Length; i++)
                hash = hash * 31 + char.ToUpperInvariant(category[i]);

            float h = Mathf.Abs(hash % 997) / 997f;
            float s = 0.35f;
            float v = EditorGUIUtility.isProSkin ? 0.85f : 0.75f;
            Color c = Color.HSVToRGB(h, s, v);
            c = Color.Lerp(c, Color.white, EditorGUIUtility.isProSkin ? 0.10f : 0.22f);
            c.a = 1f;
            return c;
        }
    }

    static string BuildManagerPersistKey(AnimatorStateEventManager manager, string statePath)
    {
        if (manager == null)
            return "null";

        string assetPath = AssetDatabase.GetAssetPath(manager);
        string guid = "";
        if (!string.IsNullOrEmpty(assetPath))
            guid = AssetDatabase.AssetPathToGUID(assetPath);

        string safeState = string.IsNullOrEmpty(statePath) ? "unknown_state" : statePath.Replace('/', '_');
        string safeGuid = string.IsNullOrEmpty(guid) ? "no_guid" : guid;

        return safeGuid + "." + safeState + ".mgr" + manager.GetInstanceID();
    }

    static bool IsFinite(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    static float FiniteOr(float v, float fallback)
    {
        return IsFinite(v) ? v : fallback;
    }

    void ShowAddEventMenuAtTime(float seconds, int insertAfterIndex = -1)
    {
        GenericMenu menu = new GenericMenu();
        AppendAddEventMenuItems(menu, "Add Event", seconds, insertAfterIndex);
        menu.ShowAsContext();
    }

    void AppendAddEventMenuItems(GenericMenu menu, string rootPath, float seconds, int insertAfterIndex)
    {
        if (menu == null)
            return;

        if (string.IsNullOrEmpty(rootPath))
            rootPath = "Add Event";

        if (_manager == null || _manager.events == null)
        {
            menu.AddDisabledItem(new GUIContent(rootPath + "/(No manager)"));
            return;
        }

        List<Type> types = GetCreatableAnimatorStateEventTypes();
        if (types.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent(rootPath + "/(No event types found)"));
            return;
        }

        for (int i = 0; i < types.Count; i++)
        {
            Type t = types[i];
            if (t == null)
                continue;

            string path = AnimatorStateEventEditorPresentation.GetMenuPath(t);
            if (string.IsNullOrWhiteSpace(path))
                path = ObjectNames.NicifyVariableName(t.Name);

            string menuPath = rootPath + "/" + path;
            float capturedSeconds = seconds;
            int capturedInsertAfter = insertAfterIndex;
            Type capturedType = t;

            menu.AddItem(new GUIContent(menuPath), false, () =>
            {
                CreateAndInsertEventAtTime(capturedType, capturedSeconds, capturedInsertAfter);
            });
        }
    }

    List<Type> GetCreatableAnimatorStateEventTypes()
    {
        var result = new List<Type>();

        foreach (Type t in TypeCache.GetTypesDerivedFrom<AnimatorStateEvent>())
        {
            if (t == null)
                continue;

            if (t.IsAbstract || t.IsInterface || t.IsGenericType)
                continue;

            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
                continue;

            if (!HasParameterlessCtor(t))
                continue;

            if (!IsSerializableManagedRefType(t))
                continue;

            result.Add(t);
        }

        result.Sort((a, b) =>
        {
            string ap = AnimatorStateEventEditorPresentation.GetMenuPath(a);
            string bp = AnimatorStateEventEditorPresentation.GetMenuPath(b);

            if (string.IsNullOrWhiteSpace(ap)) ap = a.Name;
            if (string.IsNullOrWhiteSpace(bp)) bp = b.Name;

            return string.Compare(ap, bp, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    static bool HasParameterlessCtor(Type t)
    {
        if (t == null)
            return false;

        return t.GetConstructor(Type.EmptyTypes) != null;
    }

    static bool IsSerializableManagedRefType(Type t)
    {
        if (t == null)
            return false;

        if (t.IsValueType)
            return true;

        return t.IsSerializable || Attribute.IsDefined(t, typeof(SerializableAttribute), true);
    }

    void CreateAndInsertEventAtTime(Type eventType, float seconds, int insertAfterIndex = -1)
    {
        if (_manager == null || _manager.events == null || eventType == null)
            return;

        AnimatorStateEvent evt;
        try
        {
            evt = (AnimatorStateEvent)Activator.CreateInstance(eventType);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return;
        }

        if (evt == null)
            return;

        float clipLen = _clip != null ? Mathf.Max(0f, _clip.length) : 0f;
        float t = _clip != null ? ClampAndSnapTimelineSeconds(seconds, clipLen) : Mathf.Max(0f, seconds);

        if (_clip != null)
            ApplyEventTime(evt, t);

        Undo.RecordObject(_manager, "Add Animator State Event");

        int insertIndex;
        if (insertAfterIndex >= 0 && insertAfterIndex < _manager.events.Count)
            insertIndex = Mathf.Clamp(insertAfterIndex + 1, 0, _manager.events.Count);
        else
            insertIndex = _manager.events.Count;

        _manager.events.Insert(insertIndex, evt);

        _state.playheadSeconds = t;
        SortAndKeepTrackedEvent(evt);

        EditorUtility.SetDirty(_manager);
        RebuildCategories();
        SaveStateSoon();
        Repaint();
    }

    static readonly MethodInfo _endEditingActiveTextField =
        typeof(EditorGUI).GetMethod("EndEditingActiveTextField",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    void ResetPropertyPaneEditingState()
    {
        _endEditingActiveTextField?.Invoke(null, null);
        GUI.FocusControl(null);
        GUIUtility.keyboardControl = 0;
        GUIUtility.hotControl = 0;
    }

    void SetSelectedEventIndex(int newIndex)
    {
        if (_selectedEventIndex == newIndex)
            return;

        ResetPropertyPaneEditingState();
        _selectedEventIndex = newIndex;
        _selectedEventPreviewScroll = Vector2.zero;
    }

    void SortAndKeepTrackedEvent(AnimatorStateEvent trackedEvent, bool keepDraggingIndex = false)
    {
        if (_manager == null)
            return;

        int newIndex;
        bool changed = AnimatorStateEventEditorSorting.SortManager(_manager, trackedEvent, out newIndex);

        if (changed)
            EditorUtility.SetDirty(_manager);

        if (trackedEvent != null && newIndex >= 0)
        {
            _selectedEventIndex = newIndex;

            if (keepDraggingIndex && _isDraggingEvent)
                _draggingEventIndex = newIndex;
        }
        else
        {
            if (_manager.events == null || _manager.events.Count == 0)
            {
                _selectedEventIndex = -1;
                if (keepDraggingIndex)
                    _draggingEventIndex = -1;
            }
            else
            {
                _selectedEventIndex = Mathf.Clamp(_selectedEventIndex, 0, _manager.events.Count - 1);
                if (keepDraggingIndex)
                    _draggingEventIndex = Mathf.Clamp(_draggingEventIndex, 0, _manager.events.Count - 1);
            }
        }
    }
}
#endif