using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)),
RequireComponent(typeof(CharacterController)),
RequireComponent(typeof(PlayerStatusManager)),
RequireComponent(typeof(PlayerController)),
RequireComponent(typeof(PlayerLocomotionManager)),
RequireComponent(typeof(PlayerEffectsManager)),
RequireComponent(typeof(PlayerAnimatorManager)),
RequireComponent(typeof(PlayerInventoryManager)),
RequireComponent(typeof(PlayerEquipmentManager)),
RequireComponent(typeof(PlayerInteractionManager)),
RequireComponent(typeof(PlayerCombatManager)),
RequireComponent(typeof(CharacterSoundFXManager)),
RequireComponent(typeof(CharacterVisualFXManager)),
RequireComponent(typeof(Animator))]
public class PlayerManager : CharacterManager
{
    [HideInInspector] public PlayerInteractionManager interactionManager;
    
    protected override void Awake()
    {
        base.Awake();
        interactionManager = GetComponent<PlayerInteractionManager>();

        PlayerCamera.Instance.player = this;
        PlayerUIManager.Instance.BindPlayer(this);
    }

    protected override void Start()
    {
        base.Start();

        PlayerCamera.Instance.player = this;
        characterGroup = CharacterGroup.Player;
    }

    void OnDestroy()
    {
        PlayerUIManager.Instance.BindPlayer(null);
    }

    protected override void Update()
    {
        base.Update();
    }

    public override void LoadCharacterData(ref CharacterSaveData saveData)
    {
        base.LoadCharacterData(ref saveData);

        if (statusManager is not PlayerStatusManager playerStatus) return;
        if (inventoryManager is not PlayerInventoryManager playerInventory) return;
        if (equipmentManager is not PlayerEquipmentManager playerEquipment) return;

        playerStatus.InitializeStats(
            saveData.vitality,
            saveData.endurance,
            saveData.mind,
            saveData.strength,
            saveData.dexterity
        );

        float savedHealth = saveData.health > 0f ? saveData.health : playerStatus.maxHealth;
        float savedStamina = saveData.stamina >= 0f ? saveData.stamina : playerStatus.maxStamina;
        float savedMana = saveData.mana >= 0f ? saveData.mana : playerStatus.maxMana;

        playerStatus.LoseHealth(playerStatus.currentHealth - Mathf.Min(savedHealth, playerStatus.maxHealth));
        playerStatus.LoseStamina(playerStatus.currentStamina - Mathf.Min(savedStamina, playerStatus.maxStamina));
        playerStatus.LoseMana(playerStatus.currentMana - Mathf.Min(savedMana, playerStatus.maxMana));

        List<Item> allItems = GameDatabase.Instance.GetAllItems().ToList();
        for (int i = 0; i < allItems.Count; i++)
        {
            Item item = allItems[i];
            if (GameSessionManager.Instance.SaveState.HasItem(item.id))
            {
                playerInventory.AddItem(item);
            }
        }

        if (playerInventory.GetByName("Sword") is WeaponItem weaponItem)
        {
            playerEquipment.AssignWeaponToMainHand(weaponItem);
        }
        if (playerInventory.GetByName("Potion") is WeaponItem potionItem)
        {
            playerEquipment.AssignWeaponToOffHand(potionItem);
        }
    }

    protected override void PlayCharacterDeath()
    {
        base.PlayCharacterDeath();

        StartCoroutine(RestartGame());
    }


    IEnumerator RestartGame()
    {
        yield return new WaitForSecondsRealtime(5f);
        GameManager.Instance.ReturnToTitle();
    }
}
