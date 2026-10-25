using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteractionManager : MonoBehaviour
{
    PlayerManager player;

    private List<InteractableObject> nearbyInteractables = new List<InteractableObject>();

    void Awake()
    {
        player = GetComponent<PlayerManager>();
    }
    
    private void FixedUpdate()
    {
        if (player.statusManager.isDead) return;
        if (PlayerUIManager.Instance.popUpManager.popUpWindowOpen) return;
        
        if (player.animatorManager.isInAnimation)
        {
            ClosePopUpMessage();
            return;
        }
        
        CheckForInteractions();
    }

    public void AddInteractable(InteractableObject interactable)
    {
        RefreshInteractables();
        
        if (!nearbyInteractables.Contains(interactable))
        {
            nearbyInteractables.Add(interactable);
        }
    }

    public void RemoveInteractable(InteractableObject interactable)
    {
        if (nearbyInteractables.Contains(interactable))
        {
            interactable.interactionIcon.SetActive(false);
            nearbyInteractables.Remove(interactable);
        }

        RefreshInteractables();
    }

    private void CheckForInteractions()
    {
        if (nearbyInteractables.Count == 0) return;

        InteractableObject closestInteractable = nearbyInteractables[0];
        
        if (closestInteractable == null)
        {
            nearbyInteractables.RemoveAt(0);
            return;
        }

        if (closestInteractable == null) return;

        closestInteractable.interactionIcon.SetActive(true);
        PlayerUIManager.Instance.popUpManager.ShowPopUpMessage(closestInteractable.interactionText);
    }
    
    void RefreshInteractables()
    {
        nearbyInteractables.RemoveAll(interactable => interactable == null);
    }
    
    public void Interact()
    {
        if (nearbyInteractables.Count == 0) return;

        InteractableObject closestInteractable = nearbyInteractables[0];
        
        if (closestInteractable != null)
        {
            closestInteractable.Interact(player);
            RefreshInteractables();
        }
    }

    internal void ClosePopUpMessage()
    {
        PlayerUIManager.Instance.popUpManager.ClosePopUpMessage();
    }

    public void ClearInteractables()
    {
        while (nearbyInteractables.Count > 0)
        {
            RemoveInteractable(nearbyInteractables[0]);
        }
        ClosePopUpMessage();
    }
}
