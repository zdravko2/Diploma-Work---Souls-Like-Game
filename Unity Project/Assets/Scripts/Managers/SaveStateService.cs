using System.Collections.Generic;

public class SaveStateService
{
    private readonly HashSet<string> flags = new();
    private readonly HashSet<string> ownedItemIds = new();
    private readonly HashSet<string> equippedItemIds = new();
    private readonly Dictionary<string, string> objectStates = new();

    public void LoadFromSave(CharacterSaveData saveData)
    {
        flags.Clear();
        ownedItemIds.Clear();
        equippedItemIds.Clear();
        objectStates.Clear();

        if (saveData == null)
            return;

        if (saveData.flags != null)
        {
            for (int i = 0; i < saveData.flags.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(saveData.flags[i]))
                    flags.Add(saveData.flags[i]);
            }
        }

        if (saveData.ownedItemIds != null)
        {
            for (int i = 0; i < saveData.ownedItemIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(saveData.ownedItemIds[i]))
                    ownedItemIds.Add(saveData.ownedItemIds[i]);
            }
        }

        if (saveData.equippedItemIds != null)
        {
            for (int i = 0; i < saveData.equippedItemIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(saveData.equippedItemIds[i]))
                    equippedItemIds.Add(saveData.equippedItemIds[i]);
            }
        }

        if (saveData.sceneStates != null)
        {
            for (int i = 0; i < saveData.sceneStates.Count; i++)
            {
                SceneObjectStateList sceneState = saveData.sceneStates[i];
                if (sceneState == null || string.IsNullOrWhiteSpace(sceneState.sceneId) || sceneState.objects == null)
                    continue;

                for (int j = 0; j < sceneState.objects.Count; j++)
                {
                    ObjectStateData objectState = sceneState.objects[j];
                    if (objectState == null || string.IsNullOrWhiteSpace(objectState.objectId))
                        continue;

                    objectStates[BuildObjectKey(sceneState.sceneId, objectState.objectId)] = objectState.stateJson;
                }
            }
        }
    }

    public void WriteToSave(CharacterSaveData saveData)
    {
        if (saveData == null)
            return;

        saveData.flags = new List<string>(flags);
        saveData.ownedItemIds = new List<string>(ownedItemIds);
        saveData.equippedItemIds = new List<string>(equippedItemIds);

        saveData.sceneStates ??= new List<SceneObjectStateList>();
        saveData.sceneStates.Clear();

        Dictionary<string, SceneObjectStateList> groupedStates = new();

        foreach (KeyValuePair<string, string> pair in objectStates)
        {
            SplitObjectKey(pair.Key, out string sceneId, out string objectId);

            if (!groupedStates.TryGetValue(sceneId, out SceneObjectStateList sceneState))
            {
                sceneState = new SceneObjectStateList
                {
                    sceneId = sceneId,
                    objects = new List<ObjectStateData>()
                };

                groupedStates.Add(sceneId, sceneState);
            }

            sceneState.objects.Add(new ObjectStateData
            {
                objectId = objectId,
                stateJson = pair.Value
            });
        }

        foreach (SceneObjectStateList sceneState in groupedStates.Values)
            saveData.sceneStates.Add(sceneState);
    }

    public bool HasFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId) && flags.Contains(flagId);
    }

    public bool SetFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId) && flags.Add(flagId);
    }

    public bool ClearFlag(string flagId)
    {
        return !string.IsNullOrWhiteSpace(flagId) && flags.Remove(flagId);
    }

    public bool HasItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && ownedItemIds.Contains(itemId);
    }

    public bool AddItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && ownedItemIds.Add(itemId);
    }

    public bool RemoveItem(string itemId)
    {
        equippedItemIds.Remove(itemId);
        return !string.IsNullOrWhiteSpace(itemId) && ownedItemIds.Remove(itemId);
    }

    public bool IsEquipped(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && equippedItemIds.Contains(itemId);
    }

    public bool EquipItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || !ownedItemIds.Contains(itemId))
            return false;

        return equippedItemIds.Add(itemId);
    }

    public bool UnequipItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && equippedItemIds.Remove(itemId);
    }

    public bool TryGetObjectState(string sceneId, string objectId, out string stateJson)
    {
        stateJson = null;

        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(objectId))
            return false;

        return objectStates.TryGetValue(BuildObjectKey(sceneId, objectId), out stateJson);
    }

    public void SetObjectState(string sceneId, string objectId, string stateJson)
    {
        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(objectId))
            return;

        objectStates[BuildObjectKey(sceneId, objectId)] = stateJson;
    }

    public bool RemoveObjectState(string sceneId, string objectId)
    {
        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(objectId))
            return false;

        return objectStates.Remove(BuildObjectKey(sceneId, objectId));
    }

    private static string BuildObjectKey(string sceneId, string objectId)
    {
        return sceneId + "|" + objectId;
    }

    private static void SplitObjectKey(string key, out string sceneId, out string objectId)
    {
        int index = key.LastIndexOf('|');

        if (index < 0)
        {
            sceneId = string.Empty;
            objectId = key;
            return;
        }

        sceneId = key[..index];
        objectId = key[(index + 1)..];
    }
}