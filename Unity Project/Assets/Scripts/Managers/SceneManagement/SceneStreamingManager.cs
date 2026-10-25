using System;
using System.Collections;
using System.Collections.Generic;
using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneStreamingManager : MonoBehaviour
{
    public static SceneStreamingManager Instance { get; private set; }

    public event Action<Scene> SceneLoadedByStreamer;

    public SceneDefinition CurrentLocation { get; private set; }
    public bool IsSwitching { get; private set; }

    private readonly List<string> loadedBySystem = new();
    private readonly List<string> targetScenePaths = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public IEnumerator SwitchToLocation(SceneDefinition definition)
    {
        if (definition == null || IsSwitching || CurrentLocation == definition)
            yield break;

        IsSwitching = true;
        CurrentLocation = definition;
        targetScenePaths.Clear();


        if (definition.requiredScenes != null)
        {
            for (int i = 0; i < definition.requiredScenes.Length; i++)
                AddSceneIfValid(definition.requiredScenes[i], definition.name, $"requiredScenes[{i}]");
        }

        AddSceneIfValid(definition.mainScene, definition.name, "mainScene");

        if (definition.neighbourScenes != null)
        {
            for (int i = 0; i < definition.neighbourScenes.Length; i++)
                AddSceneIfValid(definition.neighbourScenes[i], definition.name, $"neighbourScenes[{i}]");
        }

        foreach (string scenePath in targetScenePaths)
        {
            if (IsSceneLoaded(scenePath))
                continue;

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);

            if (loadOperation == null)
                continue;

            while (!loadOperation.isDone)
                yield return null;

            loadedBySystem.Add(scenePath);

            Scene loadedScene = SceneManager.GetSceneByPath(scenePath);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
                SceneLoadedByStreamer?.Invoke(loadedScene);
        }

        List<string> scenesToUnload = new();

        foreach (string scenePath in loadedBySystem)
        {
            if (!targetScenePaths.Contains(scenePath))
                scenesToUnload.Add(scenePath);
        }

        for (int i = 0; i < scenesToUnload.Count; i++)
        {
            string scenePath = scenesToUnload[i];

            if (!IsSceneLoaded(scenePath))
            {
                loadedBySystem.Remove(scenePath);
                continue;
            }

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(scenePath);

            if (unloadOperation != null)
            {
                while (!unloadOperation.isDone)
                    yield return null;
            }

            loadedBySystem.Remove(scenePath);
        }

        IsSwitching = false;
    }

    private void AddSceneIfValid(SceneReference sceneReference, string definitionName, string fieldLabel)
    {
        if (sceneReference == null)
            return;

        if (!sceneReference.TryGetPath(out string scenePath))
        {
            Debug.LogWarning($"SceneDefinition '{definitionName}' has an invalid scene reference in '{fieldLabel}'.", this);
            return;
        }

        if (!sceneReference.TryGetBuildIndex(out int buildIndex) || buildIndex < 0)
        {
            Debug.LogWarning($"Scene '{scenePath}' in SceneDefinition '{definitionName}' is not enabled in Build Settings.", this);
            return;
        }

        targetScenePaths.Add(scenePath);
    }

    private bool IsSceneLoaded(string scenePath)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        return scene.IsValid() && scene.isLoaded;
    }
}