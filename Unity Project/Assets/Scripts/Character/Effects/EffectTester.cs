using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class EffectTester : MonoBehaviour
{
    public CharacterEffect effect;

    void OnTriggerEnter(Collider other)
    {
        CharacterManager character = other.GetComponent<CharacterManager>();

        if (character == null) return;

        CharacterEffect instance = Instantiate(effect);
        character.effectsManager.ApplyEffect(instance);
    }
}
