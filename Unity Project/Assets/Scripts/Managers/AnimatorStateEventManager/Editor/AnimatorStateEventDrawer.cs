#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class AnimatorStateEventEditorClipboard
{
    [Serializable]
    sealed class EventBox
    {
        [SerializeReference] public AnimatorStateEvent value;
    }

    static EventBox s_event;
    static List<AnimatorStateEvent> s_list;

    public static bool HasEvent { get { return s_event != null && s_event.value != null; } }
    public static bool HasEventList { get { return s_list != null; } }

    public static void CopyEvent(AnimatorStateEvent src)
    {
        s_event = new EventBox { value = CloneEvent(src) };
    }

    public static bool TryGetEventClone(out AnimatorStateEvent clone)
    {
        clone = CloneEvent(s_event != null ? s_event.value : null);
        return clone != null;
    }

    public static void CopyEventList(IList<AnimatorStateEvent> src)
    {
        s_list = CloneEventList(src);
    }

    public static bool TryGetEventListClone(out List<AnimatorStateEvent> clone)
    {
        clone = s_list != null ? CloneEventList(s_list) : null;
        return clone != null;
    }

    public static List<AnimatorStateEvent> CloneEventList(IList<AnimatorStateEvent> src)
    {
        if (src == null)
            return new List<AnimatorStateEvent>();

        var dst = new List<AnimatorStateEvent>(src.Count);
        for (int i = 0; i < src.Count; i++)
            dst.Add(CloneEvent(src[i]));
        return dst;
    }

    public static AnimatorStateEvent CloneEvent(AnimatorStateEvent src)
    {
        if (src == null)
            return null;

        var type = src.GetType();
        if (!typeof(AnimatorStateEvent).IsAssignableFrom(type))
            return null;

        var clone = (AnimatorStateEvent)Activator.CreateInstance(type);
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(src), clone);
        return clone;
    }
}

internal static class AnimatorStateEventEditorPresentation
{
    struct CacheEntry
    {
        public string menuPath;
        public string displayName;
        public string category;
        public Color headerColor;
    }

    static readonly Dictionary<Type, CacheEntry> s_cache = new Dictionary<Type, CacheEntry>();

    public static string GetMenuPath(Type type)
    {
        if (type == null)
            return "Missing Event";

        return GetEntry(type).menuPath;
    }

    public static string GetListLabel(SerializedProperty element, int index)
    {
        object obj = element != null ? element.managedReferenceValue : null;
        string title = GetDisplayName(obj);
        string category = GetCategory(obj);

        if (!string.IsNullOrEmpty(category))
            return "#" + (index + 1) + " [" + category + "] " + title;

        return "#" + (index + 1) + " " + title;
    }

    public static string GetDisplayName(object obj)
    {
        if (obj == null)
            return "Missing Event";

        return GetEntry(obj.GetType()).displayName;
    }

    public static string GetCategory(object obj)
    {
        if (obj == null)
            return string.Empty;

        return GetEntry(obj.GetType()).category;
    }

    public static bool TryGetHeaderColor(SerializedProperty property, out Color color)
    {
        object obj = property != null ? property.managedReferenceValue : null;
        if (obj == null)
        {
            color = default(Color);
            return false;
        }

        return TryGetHeaderColor(obj.GetType(), out color);
    }

    public static bool TryGetHeaderColor(Type type, out Color color)
    {
        if (type == null)
        {
            color = default(Color);
            return false;
        }

        color = GetEntry(type).headerColor;
        return true;
    }

    static CacheEntry GetEntry(Type type)
    {
        CacheEntry entry;
        if (s_cache.TryGetValue(type, out entry))
            return entry;

        entry = BuildEntry(type);
        s_cache[type] = entry;
        return entry;
    }

    static CacheEntry BuildEntry(Type type)
    {
        string menuPath = ObjectNames.NicifyVariableName(type.Name);
        string displayName = menuPath;
        string category = string.Empty;

        var menuAttr = (AnimatorStateEventMenuAttribute)Attribute.GetCustomAttribute(type, typeof(AnimatorStateEventMenuAttribute));
        if (menuAttr != null && !string.IsNullOrWhiteSpace(menuAttr.path))
        {
            menuPath = menuAttr.path.Trim();

            int slash = menuPath.LastIndexOf('/');
            if (slash >= 0)
            {
                displayName = menuPath.Substring(slash + 1).Trim();
                category = menuPath.Substring(0, slash).Trim();

                int catSlash = category.IndexOf('/');
                if (catSlash >= 0)
                    category = category.Substring(0, catSlash).Trim();
            }
            else
            {
                displayName = menuPath;
            }
        }

        if (string.IsNullOrEmpty(displayName))
            displayName = ObjectNames.NicifyVariableName(type.Name);

        Color headerColor;
        if (!TryGetManualColor(type, out headerColor))
            headerColor = GetCategoryColor(category);

        return new CacheEntry
        {
            menuPath = menuPath,
            displayName = displayName,
            category = category,
            headerColor = headerColor
        };
    }

    static bool TryGetManualColor(Type type, out Color color)
    {
        var colorAttr = (AnimatorStateEventColorAttribute)Attribute.GetCustomAttribute(type, typeof(AnimatorStateEventColorAttribute));
        if (colorAttr == null)
        {
            color = default(Color);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(colorAttr.html) && ColorUtility.TryParseHtmlString(colorAttr.html, out color))
            return true;

        color = new Color(
            Mathf.Clamp01(colorAttr.r),
            Mathf.Clamp01(colorAttr.g),
            Mathf.Clamp01(colorAttr.b),
            Mathf.Clamp01(colorAttr.a));

        return true;
    }

    static Color GetCategoryColor(string category)
    {
        if (string.IsNullOrEmpty(category))
            return new Color(0.50f, 0.60f, 0.80f, 1f);

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
}

[CustomPropertyDrawer(typeof(AnimatorStateEvent), true)]
public sealed class AnimatorStateEventDrawer : PropertyDrawer
{
    const float Pad = 2f;
    const float BtnW = 18f;
    const float ToggleW = 18f;
    const float ToggleGap = 8f;
    const float HeaderAccentW = 3f;

    static readonly GUIContent GCMenu = new GUIContent("⋮", "Event actions");
    static readonly GUIContent GCCopy = new GUIContent("Copy Event");
    static readonly GUIContent GCPasteReplace = new GUIContent("Paste Event (Replace)");
    static readonly GUIContent GCPasteAfter = new GUIContent("Paste Event (Insert After)");
    static readonly GUIContent GCClone = new GUIContent("Duplicate Event");
    static readonly GUIContent GCDelete = new GUIContent("Delete Event");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;

        var enabledProp = property.FindPropertyRelative("enabled");

        bool isArrayElement = property.propertyPath.Contains(".Array.data[");
        float dragHandleSafePad = isArrayElement ? 20f : 0f;

        var headerRect = new Rect(position.x, position.y, position.width, line);

        DrawHeaderBackground(headerRect, property, enabledProp);

        var toggleRect = new Rect(headerRect.xMax - ToggleW, headerRect.y, ToggleW, line);
        var menuRect = new Rect(toggleRect.xMin - ToggleGap - BtnW, headerRect.y, BtnW, line);

        float foldoutStartX = headerRect.x + dragHandleSafePad + 2f;
        float foldoutW = Mathf.Max(0f, menuRect.xMin - foldoutStartX);
        var foldoutRect = new Rect(foldoutStartX, headerRect.y, foldoutW, line);

        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        bool canEditArray = TryGetArrayAndIndex(property, out var arrayProp, out int index);

        if (GUI.Button(menuRect, GCMenu, EditorStyles.miniButton))
            ShowEventMenu(property, canEditArray, arrayProp, index);

        if (enabledProp != null)
            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

        if (!property.isExpanded)
            return;

        AnimationClip clip = null;
        AnimatorStateEventEditorClipResolver.TryGetOwningClip(property.serializedObject.targetObject, out clip, out _);

        EditorGUI.indentLevel++;
        float y = position.y + line + Pad;

        var frequency = property.FindPropertyRelative("frequency");
        var timeMode = property.FindPropertyRelative("timeMode");
        var normalized = property.FindPropertyRelative("normalizedTime");
        var seconds = property.FindPropertyRelative("seconds");
        var frame = property.FindPropertyRelative("frame");
        var runOnInterrupt = property.FindPropertyRelative("runOnInterrupt");

        y = DrawProp(position, y, frequency);
        y = DrawProp(position, y, timeMode);

        if (timeMode != null)
        {
            var mode = (AnimatorEventTimeMode)timeMode.enumValueIndex;

            if (mode == AnimatorEventTimeMode.Seconds)
            {
                y = DrawProp(position, y, seconds);
                ClampSeconds(seconds, clip);
            }
            else if (mode == AnimatorEventTimeMode.ClipFrame)
            {
                y = DrawProp(position, y, frame);
                ClampFrame(frame, clip);
            }
            else
            {
                y = DrawProp(position, y, normalized);
                ClampNormalized(normalized);
            }
        }

        y = DrawProp(position, y, runOnInterrupt);

        DrawRemaining(position, y, property);

        EditorGUI.indentLevel--;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;

        if (!property.isExpanded)
            return line;

        float h = line + Pad;

        var frequency = property.FindPropertyRelative("frequency");
        var timeMode = property.FindPropertyRelative("timeMode");
        var normalized = property.FindPropertyRelative("normalizedTime");
        var seconds = property.FindPropertyRelative("seconds");
        var frame = property.FindPropertyRelative("frame");
        var runOnInterrupt = property.FindPropertyRelative("runOnInterrupt");

        h += GetPropHeight(frequency);
        h += GetPropHeight(timeMode);

        if (timeMode != null)
        {
            var mode = (AnimatorEventTimeMode)timeMode.enumValueIndex;

            if (mode == AnimatorEventTimeMode.Seconds)
                h += GetPropHeight(seconds);
            else if (mode == AnimatorEventTimeMode.ClipFrame)
                h += GetPropHeight(frame);
            else
                h += GetPropHeight(normalized);
        }

        h += GetPropHeight(runOnInterrupt);
        h += GetRemainingHeight(property);

        return h;
    }

    static void DrawHeaderBackground(Rect headerRect, SerializedProperty property, SerializedProperty enabledProp)
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
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, HeaderAccentW, headerRect.height), accent);
    }

    static void ShowEventMenu(SerializedProperty property, bool canEditArray, SerializedProperty arrayProp, int index)
    {
        var menu = new GenericMenu();
        object srcObj = property.managedReferenceValue;

        if (srcObj is AnimatorStateEvent srcEvent)
            menu.AddItem(GCCopy, false, () => AnimatorStateEventEditorClipboard.CopyEvent(srcEvent));
        else
            menu.AddDisabledItem(GCCopy);

        if (AnimatorStateEventEditorClipboard.HasEvent)
            menu.AddItem(GCPasteReplace, false, () => PasteReplace(property));
        else
            menu.AddDisabledItem(GCPasteReplace);

        if (canEditArray && AnimatorStateEventEditorClipboard.HasEvent)
            menu.AddItem(GCPasteAfter, false, () => PasteAfter(property, arrayProp, index));
        else
            menu.AddDisabledItem(GCPasteAfter);

        if (canEditArray && srcObj is AnimatorStateEvent)
            menu.AddItem(GCClone, false, () => CloneElement(property, arrayProp, index));
        else
            menu.AddDisabledItem(GCClone);

        if (canEditArray)
            menu.AddItem(GCDelete, false, () => DeleteElement(property, arrayProp, index));
        else
            menu.AddDisabledItem(GCDelete);

        menu.ShowAsContext();
    }

    static void PasteReplace(SerializedProperty element)
    {
        if (!AnimatorStateEventEditorClipboard.TryGetEventClone(out var ev))
            return;

        var so = element.serializedObject;
        Undo.RecordObjects(so.targetObjects, "Paste Animator State Event");

        so.UpdateIfRequiredOrScript();

        element.managedReferenceValue = ev;
        element.isExpanded = true;

        so.ApplyModifiedProperties();
        SortTargets(so);

        foreach (var obj in so.targetObjects)
            EditorUtility.SetDirty(obj);
    }

    static void PasteAfter(SerializedProperty element, SerializedProperty arrayProp, int index)
    {
        if (arrayProp == null || index < 0 || index >= arrayProp.arraySize)
            return;

        if (!AnimatorStateEventEditorClipboard.TryGetEventClone(out var ev))
            return;

        var so = element.serializedObject;
        Undo.RecordObjects(so.targetObjects, "Paste Animator State Event");

        so.UpdateIfRequiredOrScript();

        int insertAt = Mathf.Clamp(index + 1, 0, arrayProp.arraySize);
        arrayProp.InsertArrayElementAtIndex(insertAt);

        var newEl = arrayProp.GetArrayElementAtIndex(insertAt);
        newEl.managedReferenceValue = ev;
        newEl.isExpanded = true;

        so.ApplyModifiedProperties();
        SortTargets(so);

        foreach (var obj in so.targetObjects)
            EditorUtility.SetDirty(obj);
    }

    static void DeleteElement(SerializedProperty element, SerializedProperty arrayProp, int index)
    {
        var so = element.serializedObject;
        Undo.RecordObjects(so.targetObjects, "Delete Animator State Event");

        so.UpdateIfRequiredOrScript();
        arrayProp.DeleteArrayElementAtIndex(index);
        so.ApplyModifiedProperties();

        foreach (var t in so.targetObjects)
            EditorUtility.SetDirty(t);
    }

    static void CloneElement(SerializedProperty element, SerializedProperty arrayProp, int index)
    {
        var so = element.serializedObject;
        object src = element.managedReferenceValue;
        if (src == null) return;

        Undo.RecordObjects(so.targetObjects, "Clone Animator State Event");

        so.UpdateIfRequiredOrScript();

        Type t = src.GetType();
        object clone = Activator.CreateInstance(t);
        string json = JsonUtility.ToJson(src);
        JsonUtility.FromJsonOverwrite(json, clone);

        int insertAt = Mathf.Clamp(index + 1, 0, arrayProp.arraySize);
        arrayProp.InsertArrayElementAtIndex(insertAt);

        var newEl = arrayProp.GetArrayElementAtIndex(insertAt);
        newEl.managedReferenceValue = clone;
        newEl.isExpanded = true;

        so.ApplyModifiedProperties();
        SortTargets(so);

        foreach (var obj in so.targetObjects)
            EditorUtility.SetDirty(obj);
    }

    static bool TryGetArrayAndIndex(SerializedProperty element, out SerializedProperty arrayProp, out int index)
    {
        arrayProp = null;
        index = -1;

        string path = element.propertyPath;
        const string token = ".Array.data[";
        int i0 = path.LastIndexOf(token, StringComparison.Ordinal);
        if (i0 < 0) return false;

        int idxStart = i0 + token.Length;
        int idxEnd = path.IndexOf(']', idxStart);
        if (idxEnd < 0) return false;

        string idxStr = path.Substring(idxStart, idxEnd - idxStart);
        if (!int.TryParse(idxStr, out index)) return false;

        string arrayPath = path.Substring(0, i0);
        arrayProp = element.serializedObject.FindProperty(arrayPath);
        return arrayProp != null && arrayProp.isArray && index >= 0 && index < arrayProp.arraySize;
    }

    static void ClampNormalized(SerializedProperty normalized)
    {
        if (normalized == null) return;
        float v = normalized.floatValue;
        float c = Mathf.Clamp01(v);
        if (!Mathf.Approximately(v, c))
            normalized.floatValue = c;
    }

    static void ClampSeconds(SerializedProperty seconds, AnimationClip clip)
    {
        if (seconds == null) return;
        float v = seconds.floatValue;
        float max = (clip != null && clip.length > 0f) ? clip.length : float.PositiveInfinity;
        float c = Mathf.Clamp(v, 0f, max);
        if (!Mathf.Approximately(v, c))
            seconds.floatValue = c;
    }

    static void ClampFrame(SerializedProperty frame, AnimationClip clip)
    {
        if (frame == null) return;
        int v = frame.intValue;
        int max = (clip != null) ? AnimatorStateEventEditorClipResolver.GetMaxFrame(clip) : int.MaxValue;
        int c = Mathf.Clamp(v, 0, max);
        if (v != c)
            frame.intValue = c;
    }

    static float DrawProp(Rect position, float y, SerializedProperty prop)
    {
        if (prop == null) return y;

        float height = EditorGUI.GetPropertyHeight(prop, true);
        var r = new Rect(position.x, y, position.width, height);
        EditorGUI.PropertyField(r, prop, true);
        return y + height + Pad;
    }

    static float GetPropHeight(SerializedProperty prop)
    {
        if (prop == null) return 0f;
        return EditorGUI.GetPropertyHeight(prop, true) + Pad;
    }

    static bool IsBaseField(string name)
    {
        return name == "enabled" ||
               name == "frequency" ||
               name == "timeMode" ||
               name == "normalizedTime" ||
               name == "seconds" ||
               name == "frame" ||
               name == "runOnInterrupt";
    }

    static float DrawRemaining(Rect position, float y, SerializedProperty root)
    {
        var it = root.Copy();
        var end = it.GetEndProperty();

        bool enter = true;
        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            enter = false;

            if (it.depth != root.depth + 1)
                continue;

            if (IsBaseField(it.name))
                continue;

            float height = EditorGUI.GetPropertyHeight(it, true);
            var r = new Rect(position.x, y, position.width, height);
            EditorGUI.PropertyField(r, it, true);
            y += height + Pad;
        }

        return y;
    }

    static float GetRemainingHeight(SerializedProperty root)
    {
        float h = 0f;

        var it = root.Copy();
        var end = it.GetEndProperty();

        bool enter = true;
        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            enter = false;

            if (it.depth != root.depth + 1)
                continue;

            if (IsBaseField(it.name))
                continue;

            h += EditorGUI.GetPropertyHeight(it, true) + Pad;
        }

        return h;
    }

    static void SortTargets(SerializedObject so)
    {
        if (so == null)
            return;

        AnimatorStateEventEditorSorting.SortSerializedTargets(so);
        so.UpdateIfRequiredOrScript();
    }
}
#endif