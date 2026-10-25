using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Item : ScriptableObject
{
    [Header("Information")]
    public string id;
    public string itemName;
    [TextArea] public string description;
    public Sprite icon;
    [Header("Settings")]
    public bool stackable = false;

    protected virtual void Reset()
    {
        if (string.IsNullOrWhiteSpace(id)) id = System.Guid.NewGuid().ToString();
    }
}
