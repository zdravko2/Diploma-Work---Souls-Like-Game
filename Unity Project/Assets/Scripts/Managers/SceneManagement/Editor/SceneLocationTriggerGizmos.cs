#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SceneLocationTriggerGizmos
{
    private static readonly Color NormalFill = new(0.1f, 0.8f, 0.25f, 0.08f);
    private static readonly Color NormalWire = new(0.1f, 1f, 0.35f, 0.9f);

    private static readonly Color OverlapFill = new(1f, 0.45f, 0.05f, 0.12f);
    private static readonly Color OverlapWire = new(1f, 0.25f, 0.05f, 1f);

    private static readonly Color DisabledFill = new(0.5f, 0.5f, 0.5f, 0.06f);
    private static readonly Color DisabledWire = new(0.6f, 0.6f, 0.6f, 0.8f);

    private static readonly Color DuplicateSceneIdColor = new(1f, 0.1f, 0.1f, 1f);

    private static readonly Dictionary<string, int> SceneIdCounts = new();
    private static double nextCacheRefreshTime;

    static SceneLocationTriggerGizmos()
    {
        SceneView.duringSceneGui -= DrawClickableLabels;
        SceneView.duringSceneGui += DrawClickableLabels;
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawSceneLocationTrigger(SceneLocationTrigger trigger, GizmoType gizmoType)
    {
        if (!AnyLocationTriggerSelected())
            return;

        if (trigger == null)
            return;

        RefreshSceneIdCacheIfNeeded();

        Collider triggerCollider = trigger.GetComponent<Collider>();

        if (triggerCollider == null)
            return;

        SceneLocationTrigger[] allTriggers = FindAllLocationTriggers();

        bool isUsable = triggerCollider.enabled && triggerCollider.isTrigger && trigger.gameObject.activeInHierarchy;
        bool overlapsOtherTrigger = isUsable && BoundsOverlapOtherLocationTrigger(trigger, triggerCollider.bounds, allTriggers);

        Color fillColor = isUsable
            ? overlapsOtherTrigger ? OverlapFill : NormalFill
            : DisabledFill;

        Color wireColor = isUsable
            ? overlapsOtherTrigger ? OverlapWire : NormalWire
            : DisabledWire;

        DrawCollider(triggerCollider, fillColor, wireColor);
    }

    private static void DrawClickableLabels(SceneView sceneView)
    {
        if (!AnyLocationTriggerSelected())
            return;

        RefreshSceneIdCacheIfNeeded();

        SceneLocationTrigger[] allTriggers = FindAllLocationTriggers();

        Handles.BeginGUI();

        foreach (SceneLocationTrigger trigger in allTriggers)
        {
            if (trigger == null)
                continue;

            GameObject gameObject = trigger.gameObject;

            if (gameObject == null)
                continue;

            Collider triggerCollider = trigger.GetComponent<Collider>();

            if (triggerCollider == null)
                continue;

            bool isUsable = triggerCollider.enabled && triggerCollider.isTrigger && gameObject.activeInHierarchy;
            bool overlapsOtherTrigger = isUsable && BoundsOverlapOtherLocationTrigger(trigger, triggerCollider.bounds, allTriggers);

            DrawClickableLabel(sceneView, trigger, triggerCollider.bounds, overlapsOtherTrigger, isUsable);
        }

        Handles.EndGUI();
    }

    private static void DrawClickableLabel(
        SceneView sceneView,
        SceneLocationTrigger trigger,
        Bounds bounds,
        bool overlapsOtherTrigger,
        bool isUsable
    )
    {
        SceneDefinition definition = GetDefinition(trigger);
        bool hasDuplicateSceneId = HasDuplicateSceneId(definition);

        string label = GetDefinitionLabel(trigger, definition);

        if (hasDuplicateSceneId)
            label += "  DUPLICATE SCENE ID";

        if (overlapsOtherTrigger)
            label += "  OVERLAP";

        if (!isUsable)
            label += "  DISABLED / INVALID";

        Color labelColor = hasDuplicateSceneId
            ? DuplicateSceneIdColor
            : overlapsOtherTrigger ? OverlapWire : NormalWire;

        Vector3 worldPosition = bounds.center + Vector3.up * Mathf.Max(bounds.extents.y, 0.5f);

        if (!IsVisibleToSceneCamera(sceneView, worldPosition))
            return;

        Vector2 guiPosition = HandleUtility.WorldToGUIPoint(worldPosition);

        GUIStyle style = new(EditorStyles.boldLabel)
        {
            normal = { textColor = labelColor, background = null },
            hover = { textColor = labelColor, background = null },
            active = { textColor = labelColor, background = null },
            focused = { textColor = labelColor, background = null }
        };

        GUIContent content = new(label);
        Vector2 size = style.CalcSize(content);
        Rect rect = new(guiPosition.x, guiPosition.y - size.y * 0.5f, size.x + 8f, size.y);

        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        if (GUI.Button(rect, content, style))
            QueueSelectTrigger(trigger);
    }

    private static bool IsVisibleToSceneCamera(SceneView sceneView, Vector3 worldPosition)
    {
        if (sceneView == null || sceneView.camera == null)
            return false;

        Camera camera = sceneView.camera;
        Vector3 viewportPoint = camera.WorldToViewportPoint(worldPosition);

        if (viewportPoint.z <= 0f)
            return false;

        return viewportPoint.x >= 0f &&
               viewportPoint.x <= 1f &&
               viewportPoint.y >= 0f &&
               viewportPoint.y <= 1f;
    }

    private static void QueueSelectTrigger(SceneLocationTrigger trigger)
    {
        if (trigger == null || trigger.gameObject == null)
            return;

        EntityId gameObjectEntityId = trigger.gameObject.GetInstanceID();

        EditorApplication.delayCall += () =>
        {
            Object resolvedObject = EditorUtility.EntityIdToObject(gameObjectEntityId);

            if (resolvedObject is not GameObject resolvedGameObject)
                return;

            if (resolvedGameObject == null)
                return;

            if (resolvedGameObject.GetComponent<SceneLocationTrigger>() == null)
                return;

            Selection.activeGameObject = resolvedGameObject;
            EditorGUIUtility.PingObject(resolvedGameObject);
            SceneView.RepaintAll();
        };
    }

    private static SceneLocationTrigger[] FindAllLocationTriggers()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<SceneLocationTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
#else
        return Object.FindObjectsOfType<SceneLocationTrigger>(true);
#endif
    }

    private static bool AnyLocationTriggerSelected()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
                continue;

            if (selectedObject.GetComponent<SceneLocationTrigger>() != null)
                return true;
        }

        return false;
    }

    private static void RefreshSceneIdCacheIfNeeded()
    {
        if (EditorApplication.timeSinceStartup < nextCacheRefreshTime)
            return;

        nextCacheRefreshTime = EditorApplication.timeSinceStartup + 1.0d;
        SceneIdCounts.Clear();

        string[] guids = AssetDatabase.FindAssets("t:SceneDefinition");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SceneDefinition definition = AssetDatabase.LoadAssetAtPath<SceneDefinition>(path);

            if (definition == null || string.IsNullOrWhiteSpace(definition.sceneId))
                continue;

            if (!SceneIdCounts.TryAdd(definition.sceneId, 1))
                SceneIdCounts[definition.sceneId]++;
        }
    }

    private static bool BoundsOverlapOtherLocationTrigger(
        SceneLocationTrigger trigger,
        Bounds bounds,
        SceneLocationTrigger[] allTriggers
    )
    {
        foreach (SceneLocationTrigger other in allTriggers)
        {
            if (other == null || other == trigger)
                continue;

            if (other.gameObject == null || !other.gameObject.scene.IsValid())
                continue;

            Collider otherCollider = other.GetComponent<Collider>();

            if (otherCollider == null || !otherCollider.enabled || !other.gameObject.activeInHierarchy)
                continue;

            if (bounds.Intersects(otherCollider.bounds))
                return true;
        }

        return false;
    }

    private static void DrawCollider(Collider collider, Color fillColor, Color wireColor)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;

        if (collider is BoxCollider box)
        {
            Transform t = box.transform;
            Gizmos.matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);

            Gizmos.color = fillColor;
            Gizmos.DrawCube(box.center, box.size);

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (collider is SphereCollider sphere)
        {
            Transform t = sphere.transform;
            Gizmos.matrix = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale);

            Gizmos.color = fillColor;
            Gizmos.DrawSphere(sphere.center, sphere.radius);

            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
        else
        {
            Gizmos.matrix = Matrix4x4.identity;

            Bounds bounds = collider.bounds;

            Gizmos.color = fillColor;
            Gizmos.DrawCube(bounds.center, bounds.size);

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        Gizmos.matrix = previousMatrix;
    }

    private static bool HasDuplicateSceneId(SceneDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.sceneId))
            return false;

        return SceneIdCounts.TryGetValue(definition.sceneId, out int count) && count > 1;
    }

    private static string GetDefinitionLabel(SceneLocationTrigger trigger, SceneDefinition definition)
    {
        if (definition == null)
            return trigger.name;

        if (!string.IsNullOrWhiteSpace(definition.name))
            return definition.name;

        return trigger.name;
    }

    private static SceneDefinition GetDefinition(SceneLocationTrigger trigger)
    {
        if (trigger == null)
            return null;

        SerializedObject serializedObject = new(trigger);
        SerializedProperty definitionProperty = serializedObject.FindProperty("sceneDefinition");

        if (definitionProperty == null)
            return null;

        return definitionProperty.objectReferenceValue as SceneDefinition;
    }
}
#endif