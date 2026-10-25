using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager Instance;
    private PlayerManager player;
    [HideInInspector] public PlayerHUDManager HUDManager;
    [HideInInspector] public UIEffectsManager effectsUIManager;
    [HideInInspector] public PlayerUIPopUpManager popUpManager;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }

        player = PlayerCamera.Instance.player;
        HUDManager = GetComponentInChildren<PlayerHUDManager>();
        effectsUIManager = GetComponentInChildren<UIEffectsManager>();
        popUpManager = GetComponentInChildren<PlayerUIPopUpManager>();

        //ToggleHUD(false);
    }

    PlayerStatusManager statusManager;
    PlayerEffectsManager effectsManager;
    public void BindPlayer(PlayerManager player)
    {
        if (statusManager != null)
        {
            statusManager.OnHealthChanged -= HUDManager.SetHealth;
            statusManager.OnMaxHealthChanged -= HUDManager.SetMaxHealth;
            statusManager.OnStaminaChanged -= HUDManager.SetStamina;
            statusManager.OnMaxStaminaChanged -= HUDManager.SetMaxStamina;
            statusManager.OnManaChanged -= HUDManager.SetMana;
            statusManager.OnMaxManaChanged -= HUDManager.SetMaxMana;
        }

        if (effectsManager != null)
        {
            effectsManager.OnEffectStarted -= effectsUIManager.AddIcon;
            effectsManager.OnEffectEnded -= effectsUIManager.RemoveIcon;
        }


        if (player == null) return;

        statusManager = player.statusManager as PlayerStatusManager;
        effectsManager = player.effectsManager as PlayerEffectsManager;

        if (statusManager == null) return;
        if (effectsManager == null) return;

        statusManager.OnHealthChanged += HUDManager.SetHealth;
        statusManager.OnMaxHealthChanged += HUDManager.SetMaxHealth;
        statusManager.OnStaminaChanged += HUDManager.SetStamina;
        statusManager.OnMaxStaminaChanged += HUDManager.SetMaxStamina;
        statusManager.OnManaChanged += HUDManager.SetMana;
        statusManager.OnMaxManaChanged += HUDManager.SetMaxMana;

        effectsManager.OnEffectStarted += effectsUIManager.AddIcon;
        effectsManager.OnEffectEnded += effectsUIManager.RemoveIcon;

        HUDManager.SetMaxHealth(statusManager.maxHealth);
        HUDManager.SetHealth(statusManager.currentHealth);
        HUDManager.SetMaxStamina(statusManager.maxStamina);
        HUDManager.SetStamina(statusManager.currentStamina);
        HUDManager.SetMaxMana(statusManager.maxMana);
        HUDManager.SetMana(statusManager.currentMana);
    }
    
    public void ToggleHUD(bool active)
    {
        HUDManager.gameObject.SetActive(active);
        effectsManager.gameObject.SetActive(active);
        popUpManager.gameObject.SetActive(active);
    }
}
