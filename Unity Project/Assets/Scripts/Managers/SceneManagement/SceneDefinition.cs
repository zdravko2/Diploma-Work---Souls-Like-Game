using Eflatun.SceneReference;
using UnityEngine;

[CreateAssetMenu(menuName = "Level Streaming/Scene Definition")]
public class SceneDefinition : ScriptableObject
{
    [Header("Identity")]
    public string sceneId;
    public string displayName;
    public string defaultSpawnPointId = "Start";

    [Header("Scene References")]
    public SceneReference mainScene;
    public SceneReference[] neighbourScenes;
    public SceneReference[] requiredScenes;

    void Reset()
    {
        if (string.IsNullOrWhiteSpace(sceneId)) sceneId = System.Guid.NewGuid().ToString();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
    }
}