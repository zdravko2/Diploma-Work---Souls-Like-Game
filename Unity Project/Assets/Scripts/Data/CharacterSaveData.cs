using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterSaveData
{
    public int version = 1;

    [Header("Save Metadata")]
    public string saveFileName;
    public string saveDisplayName;
    public string locationDisplayName;
    public string savedAtUtc;

    [Header("Character")]
    public string characterName;

    [Header("Location")]
    public string currentSceneId;
    public string currentSpawnPointId;

    [Header("Stats")]
    public int vitality = 1;
    public int endurance = 1;
    public int mind = 1;
    public int strength = 1;
    public int dexterity = 1;

    [Header("Current Vitals")]
    public float health = 0f;
    public float stamina = 0f;
    public float mana = 0f;

    [Header("Fallback Transform")]
    public float xPosition = 0f;
    public float yPosition = 0f;
    public float zPosition = 0f;
    public float xRotation = 0f;
    public float yRotation = 0f;
    public float zRotation = 0f;

    [Header("Progression")]
    public List<string> flags = new();
    public List<string> ownedItemIds = new();
    public List<string> equippedItemIds = new();

    [Header("Scene Object State")]
    public List<SceneObjectStateList> sceneStates = new();
}

[Serializable]
public class SceneObjectStateList
{
    public string sceneId;
    public List<ObjectStateData> objects = new();
}

[Serializable]
public class ObjectStateData
{
    public string objectId;
    public string stateJson;
}