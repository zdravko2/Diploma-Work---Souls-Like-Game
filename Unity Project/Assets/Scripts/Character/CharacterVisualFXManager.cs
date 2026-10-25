using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterVisualFXManager : MonoBehaviour
{
    CharacterManager character;
    public GameObject damageEffect;
    public GameObject blockEffect;
    public GameObject parryEffect;
    public GameObject healEffect;

    protected virtual void Awake()
    {
        character = GetComponent<CharacterManager>();
    }

    public virtual GameObject PlayVisualEffectLocalSpace( GameObject particleEffect, Vector3 localPosition, Vector3 localRotation, bool follow = false, float duration = 1f)
    {
        if (particleEffect != null)
        {
            Vector3 worldPos = character.transform.TransformPoint(localPosition);

            GameObject instance = Instantiate(
                particleEffect,
                worldPos,
                Quaternion.Euler(localRotation) * character.transform.rotation,
                follow ? character.transform : null
            );

            Destroy(instance, duration);
            return instance;
        }
        return null;
    }

    public virtual GameObject PlayVisualEffectWorldSpace(GameObject particleEffect, Vector3 worldPosition, Vector3 worldRotation, bool follow = false, float duration = 1f)
    {
        if (particleEffect != null)
        {
            GameObject instance = Instantiate(
                particleEffect,
                worldPosition,
                Quaternion.Euler(worldRotation),
                follow ? character.transform : null
            );

            Destroy(instance, duration);
            return instance;
        }
        return null;
    }
}
