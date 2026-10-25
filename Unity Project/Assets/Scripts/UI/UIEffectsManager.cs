using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIEffectsManager : MonoBehaviour
{
    public GameObject iconPrefab;
    public Vector3 iconScale = Vector3.one;
    private readonly Dictionary<CharacterEffect, UIEffectIcon> icons =
      new Dictionary<CharacterEffect, UIEffectIcon>();

    public void AddIcon(CharacterEffect effect)
    {
        if (icons.ContainsKey(effect) || effect.displayIcon == false) return;
        
        GameObject iconIUPrefab = Instantiate(iconPrefab, this.transform);
        UIEffectIcon ui = iconIUPrefab.GetComponent<UIEffectIcon>();
        ui.Initialize(effect);
        iconIUPrefab.transform.localScale = iconScale;
        icons.Add(effect, ui);
    }

    public void RemoveIcon(CharacterEffect effect)
    {
        if (!icons.TryGetValue(effect, out var ui)) return;

        Destroy(ui.gameObject);
        icons.Remove(effect);
    }
}
