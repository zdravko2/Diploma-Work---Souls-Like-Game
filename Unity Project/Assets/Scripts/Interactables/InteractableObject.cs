using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public abstract class InteractableObject : MonoBehaviour
{
    [HideInInspector] public string interactionText = "Interact";
    [SerializeField] protected Collider interactionCollider;
    public GameObject interactionIcon;

    protected virtual void Awake()
    {
        if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
        interactionCollider.isTrigger = true;
        interactionCollider.enabled = true;
        if (interactionIcon == null) interactionIcon = GetComponentInChildren<WorldSpaceUIManager>().gameObject;
        if (interactionIcon != null) interactionIcon.SetActive(false);
        else Debug.LogWarning($"Interaction icon is not set for {gameObject.name} in {gameObject.scene.path}. No icon will be shown when player can interact with this object.", this);

        Reset();
    }

    void Reset()
    {
        gameObject.layer = LayerMask.NameToLayer("Interactable");
    }

    public virtual void Interact(PlayerManager player)
    {
        player.interactionManager.ClosePopUpMessage();
    }

    public virtual void OnTriggerEnter(Collider other)
    {
        PlayerManager player = other.GetComponent<PlayerManager>();

        if (player == null) return;

        player.interactionManager.AddInteractable(this);
    }

    public virtual void OnTriggerExit(Collider other)
    {
        PlayerManager player = other.GetComponent<PlayerManager>();

        if (player == null) return;
        if (player.interactionManager == null) return;

        player.interactionManager.RemoveInteractable(this);
        player.interactionManager.ClosePopUpMessage();
    }
}
