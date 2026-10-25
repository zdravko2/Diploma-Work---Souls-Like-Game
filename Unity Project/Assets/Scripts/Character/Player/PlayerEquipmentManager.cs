using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEquipmentManager : CharacterEquipmentManager
{
    [Header("Off Hand Slots")]
    [SerializeField] protected WeaponModelSlot offHandSlot;
    [SerializeField] protected WeaponModelSlot offHandDummySlot;

    public WeaponItem offHandWeaponItem { get; protected set; }
    public WeaponManager offHandWeaponManager { get; protected set; }
    public GameObject offHandWeaponObject { get; protected set; }
    public bool hasOffHandWeapon => offHandWeaponItem != null;
    public bool isOffHandWeaponHeld => offHandWeaponManager != null;

    public bool AssignWeaponToOffHand(WeaponItem weaponItem)
    {
        if (weaponItem == null) return false;
        if (offHandDummySlot == null) return false;

        offHandWeaponItem = weaponItem;

        if (offHandSlot != null)
            offHandSlot.UnloadModel();

        offHandDummySlot.UnloadModel();

        offHandWeaponObject = offHandDummySlot.LoadModel(offHandWeaponItem);
        offHandWeaponManager = null;

        return offHandWeaponObject != null;
    }

    public void ClearOffHandWeapon()
    {
        if (offHandSlot != null)
            offHandSlot.UnloadModel();

        if (offHandDummySlot != null)
            offHandDummySlot.UnloadModel();

        offHandWeaponItem = null;
        offHandWeaponManager = null;
        offHandWeaponObject = null;
    }

    public bool EquipOffHandWeapon()
    {
        if (offHandWeaponItem == null) return false;
        if (offHandSlot == null) return false;

        if (offHandDummySlot != null)
            offHandDummySlot.UnloadModel();

        offHandSlot.UnloadModel();
        offHandWeaponObject = offHandSlot.LoadModel(offHandWeaponItem);

        if (offHandWeaponObject == null) return false;

        offHandWeaponManager = offHandWeaponObject.GetComponent<WeaponManager>();
        if (offHandWeaponManager != null)
            offHandWeaponManager.SetWeaponItem(offHandWeaponItem, character);

        return true;
    }

    public bool UnequipOffHandWeapon()
    {
        if (offHandWeaponItem == null) return false;
        if (offHandDummySlot == null) return false;

        if (offHandSlot != null)
            offHandSlot.UnloadModel();

        offHandDummySlot.UnloadModel();
        offHandWeaponObject = offHandDummySlot.LoadModel(offHandWeaponItem);
        offHandWeaponManager = null;

        return offHandWeaponObject != null;
    }
}
