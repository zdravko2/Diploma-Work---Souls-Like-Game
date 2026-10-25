using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillPlane : MonoBehaviour
{
    private Collider killBox;

    void OnTriggerEnter(Collider other)
    {
        CharacterManager character = other.GetComponent<CharacterManager>();
        if (character == null) return;

        InstantMeleeWeaponDamageEffect damageEffect = ScriptableObject.CreateInstance<InstantMeleeWeaponDamageEffect>();
        damageEffect.damage = new DamageData(physical: 10000000);
        character.effectsManager.ApplyEffect(damageEffect);
    }
}
