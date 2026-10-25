#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Callbacks;
using UnityEngine;

[InitializeOnLoad]
static class AnimatorStateEventMissingTypeValidator
{
    static AnimatorStateEventMissingTypeValidator()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [DidReloadScripts]
    static void ValidateAfterScriptsReload()
    {
        ValidateAllControllers();
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            ValidateAllControllers();
    }

    static void ValidateAllControllers()
    {
        var loggedThisScan = new HashSet<string>();

        string[] guids = AssetDatabase.FindAssets("t:AnimatorController");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                continue;

            ValidateController(controller, path, loggedThisScan);
        }
    }

    static void ValidateController(
        AnimatorController controller,
        string controllerAssetPath,
        HashSet<string> loggedThisScan)
    {
        for (int layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
            ValidateStateMachine(
                controller.layers[layerIndex].stateMachine,
                controller,
                controllerAssetPath,
                "",
                loggedThisScan);
    }

    static void ValidateStateMachine(
        AnimatorStateMachine stateMachine,
        AnimatorController controller,
        string controllerAssetPath,
        string parentPath,
        HashSet<string> loggedThisScan)
    {
        if (stateMachine == null)
            return;

        for (int i = 0; i < stateMachine.states.Length; i++)
        {
            ChildAnimatorState child = stateMachine.states[i];
            AnimatorState state = child.state;
            if (state == null)
                continue;

            string statePath = string.IsNullOrEmpty(parentPath)
                ? state.name
                : parentPath + "/" + state.name;

            var behaviours = state.behaviours;
            if (behaviours == null)
                continue;

            for (int b = 0; b < behaviours.Length; b++)
            {
                AnimatorStateEventManager manager = behaviours[b] as AnimatorStateEventManager;
                if (manager == null)
                    continue;

                ValidateManager(manager, controller, controllerAssetPath, statePath, loggedThisScan);
            }
        }

        for (int i = 0; i < stateMachine.stateMachines.Length; i++)
        {
            ChildAnimatorStateMachine child = stateMachine.stateMachines[i];
            if (child.stateMachine == null)
                continue;

            string subPath = string.IsNullOrEmpty(parentPath)
                ? child.stateMachine.name
                : parentPath + "/" + child.stateMachine.name;

            ValidateStateMachine(child.stateMachine, controller, controllerAssetPath, subPath, loggedThisScan);
        }
    }

    static void ValidateManager(
        AnimatorStateEventManager manager,
        AnimatorController controller,
        string controllerAssetPath,
        string resolvedStatePath,
        HashSet<string> loggedThisScan)
    {
        if (manager == null)
            return;

        SerializedObject so = new SerializedObject(manager);
        SerializedProperty statePathProp = so.FindProperty("statePath");

        if (statePathProp != null && statePathProp.stringValue != resolvedStatePath)
        {
            statePathProp.stringValue = resolvedStatePath;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(controller);
        }

        if (!SerializationUtility.HasManagedReferencesWithMissingTypes(manager))
            return;

        foreach (var missing in SerializationUtility.GetManagedReferencesWithMissingTypes(manager))
        {
            string missingTypeName = string.IsNullOrWhiteSpace(missing.namespaceName)
                ? missing.className
                : missing.namespaceName + "." + missing.className;

            string warningKey =
                controllerAssetPath + "|" +
                resolvedStatePath + "|" +
                missing.assemblyName + "|" +
                missingTypeName + "|" +
                missing.referenceId;

            if (!loggedThisScan.Add(warningKey))
                continue;

            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, controller,
            $"Missing animator state event type '{missingTypeName}' in controller '{controllerAssetPath}', state '{resolvedStatePath}'.\nForce rebuild to remove warning.\n");
        }
    }
}
#endif