using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponModelSlot : MonoBehaviour
{
    private GameObject weaponObject;

    public GameObject LoadModel(WeaponItem weaponItem)
    {
        UnloadModel();
        if (weaponItem == null) return null;

        weaponObject = Instantiate(weaponItem.model);
        weaponObject.transform.SetParent(transform);
        weaponObject.transform.localPosition = Vector3.zero;
        weaponObject.transform.localRotation = Quaternion.identity;
        
        return weaponObject;
    }

    public void UnloadModel()
    {
        if (weaponObject != null)
        {
            Destroy(weaponObject);
            weaponObject = null;
        }
    }
}
