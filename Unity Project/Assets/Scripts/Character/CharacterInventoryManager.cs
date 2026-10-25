using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterInventoryManager : MonoBehaviour
{
    public List<Item> items = new List<Item>();

    public bool AddItem(Item item)
    {
        if (item == null || items.Contains(item)) return false;
        items.Add(item);
        return true;
    }

    public bool RemoveItem(Item item)
    {
        return item != null && items.Remove(item);
    }

    public bool ContainsItem(Item item)
    {
        return item != null && items.Contains(item);
    }

    public Item GetById(string id)
    {
        foreach (Item item in items)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public Item GetByName(string name)
    {
        foreach (Item item in items)
        {
            if (item.itemName == name) return item;
        }
        return null;
    }

    public Item GetByIndex(int index)
    {
        if (index < items.Count) return items[index];
        return null;
    }
    
    public Item GetByType<T>() where T : Item
    {
        foreach (Item item in items)
        {
            if (item is T typedItem) return typedItem;
        }
        return null;
    }
}
