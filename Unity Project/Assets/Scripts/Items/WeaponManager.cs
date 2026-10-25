using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    [Header("Information")]
    public WeaponItem weaponItem;
    private CharacterManager owner;
    private List<HitBox> hitBoxes = new List<HitBox>();

    public void SetWeaponItem(WeaponItem weaponItem, CharacterManager owner)
    {
        this.weaponItem = weaponItem;
        this.owner = owner;

        SetupHitBoxes();
        SetupOnHitEffects();
    }

    void SetupHitBoxes()
    {
        hitBoxes = gameObject.GetComponentsInChildren<HitBox>().ToList();

        foreach (var hitBox in hitBoxes)
        {
            hitBox.owner = owner;
            owner.combatManager.HandleHitBoxRegistration(hitBox, true);
        }
    }

    void SetupOnHitEffects()
    {
        foreach (HitBox hitBox in hitBoxes)
        {
            hitBox.baseDamage = weaponItem.damageData;
            hitBox.onHitEffects = weaponItem.onHitEffects;
        }
    }

    void OnDestroy()
    {
        foreach (var hitBox in hitBoxes)
        {
            hitBox.owner = owner;
            owner.combatManager.HandleHitBoxRegistration(hitBox, false);
        }
    }
}