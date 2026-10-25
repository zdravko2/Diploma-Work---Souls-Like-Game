using UnityEngine;

[DisallowMultipleComponent]
public class CharacterEquipmentManager : MonoBehaviour
{
    protected CharacterManager character;
    protected CharacterInventoryManager inventoryManager;

    [Header("Main Hand Slots")]
    [SerializeField] protected WeaponModelSlot mainHandSlot;
    [SerializeField] protected WeaponModelSlot mainHandDummySlot;

    public WeaponItem mainHandWeaponItem { get; protected set; }
    public WeaponManager mainHandWeaponManager { get; protected set; }
    public GameObject mainHandWeaponObject { get; protected set; }
    public bool hasMainHandWeapon => mainHandWeaponItem != null;
    public bool isMainHandWeaponHeld => mainHandWeaponManager != null;

    protected virtual void Awake()
    {
        character = GetComponent<CharacterManager>();
        inventoryManager = GetComponent<CharacterInventoryManager>();
    }

    public bool AssignWeaponToMainHand(WeaponItem weaponItem)
    {
        if (weaponItem == null) return false;
        if (mainHandDummySlot == null) return false;

        mainHandWeaponItem = weaponItem;

        if (mainHandSlot != null)
            mainHandSlot.UnloadModel();

        mainHandDummySlot.UnloadModel();

        mainHandWeaponObject = mainHandDummySlot.LoadModel(mainHandWeaponItem);
        mainHandWeaponManager = null;

        return mainHandWeaponObject != null;
    }

    public void ClearMainHandWeapon()
    {
        if (mainHandSlot != null)
            mainHandSlot.UnloadModel();

        if (mainHandDummySlot != null)
            mainHandDummySlot.UnloadModel();

        mainHandWeaponItem = null;
        mainHandWeaponManager = null;
        mainHandWeaponObject = null;
    }

    public bool EquipMainHandWeapon()
    {
        if (mainHandWeaponItem == null) return false;
        if (mainHandSlot == null) return false;

        if (mainHandDummySlot != null)
            mainHandDummySlot.UnloadModel();

        mainHandSlot.UnloadModel();
        mainHandWeaponObject = mainHandSlot.LoadModel(mainHandWeaponItem);

        if (mainHandWeaponObject == null) return false;

        mainHandWeaponManager = mainHandWeaponObject.GetComponent<WeaponManager>();
        if (mainHandWeaponManager != null)
            mainHandWeaponManager.SetWeaponItem(mainHandWeaponItem, character);

        return true;
    }

    public bool UnequipMainHandWeapon()
    {
        if (mainHandWeaponItem == null) return false;
        if (mainHandDummySlot == null) return false;

        if (mainHandSlot != null)
            mainHandSlot.UnloadModel();

        mainHandDummySlot.UnloadModel();
        mainHandWeaponObject = mainHandDummySlot.LoadModel(mainHandWeaponItem);
        mainHandWeaponManager = null;

        return mainHandWeaponObject != null;
    }
}