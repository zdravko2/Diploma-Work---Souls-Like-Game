using System.Collections.Generic;
using UnityEngine;

public class GameDatabase : MonoBehaviour
{
    public static GameDatabase Instance { get; private set; }

    [Header("Items")]
    [SerializeField] private List<Item> items = new();

    [Header("Gameplay Locations")]
    [SerializeField] private List<SceneDefinition> gameplayLocations = new();

    private readonly Dictionary<string, Item> itemLookup = new();
    private readonly Dictionary<string, SceneDefinition> locationLookup = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        RebuildLookups();
    }

    public void RebuildLookups()
    {
        itemLookup.Clear();
        locationLookup.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];

            if (item == null)
                continue;

            if (string.IsNullOrWhiteSpace(item.id))
            {
                Debug.LogWarning($"Item '{item.name}' is missing id.", item);
                continue;
            }

            if (itemLookup.ContainsKey(item.id))
            {
                Debug.LogWarning($"Duplicate item id '{item.id}' found on '{item.name}'.", item);
                continue;
            }

            itemLookup.Add(item.id, item);
        }

        for (int i = 0; i < gameplayLocations.Count; i++)
        {
            SceneDefinition location = gameplayLocations[i];

            if (location == null)
                continue;

            if (string.IsNullOrWhiteSpace(location.sceneId))
            {
                Debug.LogWarning($"SceneDefinition '{location.name}' is missing sceneId.", location);
                continue;
            }

            if (locationLookup.ContainsKey(location.sceneId))
            {
                Debug.LogWarning($"Duplicate sceneId '{location.sceneId}' found on '{location.name}'.", location);
                continue;
            }

            locationLookup.Add(location.sceneId, location);
        }
    }

    public bool TryGetItem(string id, out Item item)
    {
        item = null;

        if (string.IsNullOrWhiteSpace(id))
            return false;

        return itemLookup.TryGetValue(id, out item);
    }

    public Item GetItem(string id)
    {
        return TryGetItem(id, out Item item) ? item : null;
    }

    public bool HasItem(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && itemLookup.ContainsKey(id);
    }

    public bool TryGetLocation(string sceneId, out SceneDefinition location)
    {
        location = null;

        if (string.IsNullOrWhiteSpace(sceneId))
            return false;

        return locationLookup.TryGetValue(sceneId, out location);
    }

    public SceneDefinition GetLocation(string sceneId)
    {
        return TryGetLocation(sceneId, out SceneDefinition location) ? location : null;
    }

    public bool HasLocation(string sceneId)
    {
        return !string.IsNullOrWhiteSpace(sceneId) && locationLookup.ContainsKey(sceneId);
    }

    public IReadOnlyList<Item> GetAllItems()
    {
        return items;
    }

    public IReadOnlyList<SceneDefinition> GetAllLocations()
    {
        return gameplayLocations;
    }
}