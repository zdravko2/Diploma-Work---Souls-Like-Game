using System.Collections;
using UnityEngine;

public class GrabbableObject : InteractableObject, ISavableObject
{
    private GameObject itemObject;
    [SerializeField] private string saveId;
    [SerializeField] private WeaponItem item;
    [SerializeField] private bool isCollected = false;
    [SerializeField] private bool assignToMainHand = false;
    [SerializeField] private bool assignToOffHand = true;
    [SerializeField] private bool equipInHand = false;
    [SerializeField] private GrabPosition grabPosition = GrabPosition.Low;
    private float assignDelay = 0;

    public enum GrabPosition
    {
        Low,
        Medium,
        High,
    }

    public string SaveId => saveId;

    private void Start()
    {
        interactionText = "Pick Up";
        itemObject = Instantiate(item.model, transform);
        itemObject.transform.parent = transform;

        if (string.IsNullOrWhiteSpace(saveId))
            Debug.LogWarning($"Save ID is not set for {gameObject.name} in {gameObject.scene.path}.", this);
        if (string.IsNullOrWhiteSpace(item.id))
            Debug.LogWarning($"Item ID is not set for {gameObject.name} in {gameObject.scene.path}.", this);

        if (GameSessionManager.Instance != null && !string.IsNullOrWhiteSpace(saveId))
        {
            string sceneId = gameObject.scene.path;

            if (GameSessionManager.Instance.SaveState.TryGetObjectState(sceneId, saveId, out string stateJson))
            {
                RestoreState(stateJson);
            }
            else
            {
                GameSessionManager.Instance.SaveState.SetObjectState(sceneId, saveId, CaptureState());
            }

            if (!string.IsNullOrWhiteSpace(item.id) && GameSessionManager.Instance.SaveState.HasItem(item.id))
            {
                isCollected = true;
            }
        }

        StartCoroutine(ApplyState());
    }
    void Reset()
    {
        if (string.IsNullOrWhiteSpace(saveId)) saveId = System.Guid.NewGuid().ToString();
    }

    public override void Interact(PlayerManager player)
    {
        if (isCollected)
            return;

        base.Interact(player);
        
        switch (grabPosition)
        {
            case GrabPosition.Low:
                player.animatorManager.PlayTargetAnimation(player.animatorManager.animations.GrabLow);
                assignDelay = 1.5f;
                break;
            case GrabPosition.Medium:
                player.animatorManager.PlayTargetAnimation(player.animatorManager.animations.GrabMedium);
                assignDelay = 0.3f;
                break;
            case GrabPosition.High:
                player.animatorManager.PlayTargetAnimation(player.animatorManager.animations.GrabHigh);
                assignDelay = 0.5f;
                break;
        }

        isCollected = true;

        if (GameSessionManager.Instance != null)
        {
            if (!string.IsNullOrWhiteSpace(item.id))
                GameSessionManager.Instance.SaveState.AddItem(item.id);

            if (!string.IsNullOrWhiteSpace(saveId))
                GameSessionManager.Instance.SaveState.SetObjectState(gameObject.scene.path, saveId, CaptureState());
        }

        if (player != null && player.interactionManager != null)
        {
            player.interactionManager.RemoveInteractable(this);
            player.interactionManager.ClosePopUpMessage();
        }

        if (interactionIcon != null)
            interactionIcon.SetActive(false);

        StartCoroutine(ApplyState(player));
    }

    private IEnumerator ApplyState(PlayerManager player = null)
    {
        yield return new WaitForSeconds(assignDelay);
        
        if (itemObject != null)
            itemObject.SetActive(!isCollected);

        if (interactionCollider != null)
            interactionCollider.enabled = !isCollected;
        if (player != null && player.inventoryManager != null)
        {
            if (isCollected)
            {
                player.inventoryManager.AddItem(item);
                if (assignToMainHand)
                {
                    if (player.equipmentManager is not PlayerEquipmentManager equipmentManager) yield break;
                    equipmentManager.AssignWeaponToMainHand(item as WeaponItem);
                    if (equipInHand) equipmentManager.EquipMainHandWeapon();
                }
                else if (assignToOffHand)
                {
                    if (player.equipmentManager is not PlayerEquipmentManager equipmentManager) yield break;
                    equipmentManager.AssignWeaponToOffHand(item as WeaponItem);
                    if (equipInHand) equipmentManager.EquipOffHandWeapon();
                }
            }
            else
            {
                player.inventoryManager.RemoveItem(player.inventoryManager.GetById(item.id));
            }
        }
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(new SaveData
        {
            isCollected = isCollected,
            itemId = item.id
        });
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            StartCoroutine(ApplyState());
            return;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(stateJson);
        isCollected = data.isCollected;

        StartCoroutine(ApplyState());
    }

    [System.Serializable]
    private struct SaveData
    {
        public bool isCollected;
        public string itemId;
    }
}